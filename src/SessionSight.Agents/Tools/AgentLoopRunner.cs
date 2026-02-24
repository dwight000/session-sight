using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SessionSight.Agents.Helpers;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Runs an agent loop that allows the LLM to call tools until completion.
/// </summary>
public partial class AgentLoopRunner
{
    public const int MaxToolCalls = 15;
    public static readonly TimeSpan LoopTimeout = TimeSpan.FromMinutes(5);

    private readonly IEnumerable<IAgentTool> _tools;
    private readonly ILogger<AgentLoopRunner> _logger;

    public AgentLoopRunner(IEnumerable<IAgentTool> tools, ILogger<AgentLoopRunner> logger)
    {
        _tools = tools;
        _logger = logger;
    }

    public Task<AgentLoopResult> RunAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        CancellationToken ct = default)
    {
        return RunCoreAsync(chatClient, messages, _tools, null, null, ct);
    }

    public Task<AgentLoopResult> RunAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        ChatResponseFormat? responseFormat,
        float? temperature = null,
        CancellationToken ct = default)
    {
        return RunCoreAsync(chatClient, messages, _tools, responseFormat, temperature, ct);
    }

    public Task<AgentLoopResult> RunAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        IEnumerable<IAgentTool> tools,
        float? temperature = null,
        CancellationToken ct = default)
    {
        return RunCoreAsync(chatClient, messages, tools, null, temperature, ct);
    }

#pragma warning disable S3776 // Cognitive complexity - agent loop requires sequential control flow
    private async Task<AgentLoopResult> RunCoreAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        IEnumerable<IAgentTool> tools,
        ChatResponseFormat? responseFormat,
        float? temperature,
        CancellationToken ct)
    {
#pragma warning restore S3776
        var toolCallCount = 0;
        var trace = new List<ToolCallEntry>();
        var llmTraces = new List<LlmCallTrace>();
        var toolArray = tools as IAgentTool[] ?? tools.ToArray();
        var toolList = toolArray.ToChatTools().ToList();
        var loopRound = 0;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        var totalTotalTokens = 0;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(LoopTimeout);
        var linkedToken = timeoutCts.Token;

        try
        {
            while (true)
            {
                // Check tool limit BEFORE making call
                if (toolCallCount >= MaxToolCalls)
                {
                    LogToolCallLimitHit(_logger, MaxToolCalls);
                    return AgentLoopResult.Partial(
                        $"Tool limit ({MaxToolCalls}) exceeded - extraction incomplete",
                        toolCallCount,
                        trace,
                        totalInputTokens, totalOutputTokens, totalTotalTokens,
                        llmTraces);
                }

                var options = new ChatCompletionOptions();
                if (responseFormat is not null)
                {
                    options.ResponseFormat = responseFormat;
                }
                if (temperature.HasValue)
                {
                    options.Temperature = temperature.Value;
                }
                foreach (var tool in toolList)
                {
                    options.Tools.Add(tool);
                }

                var llmSw = Stopwatch.StartNew();
                var response = await chatClient.CompleteChatAsync(messages, options, linkedToken);
                llmSw.Stop();
                var completion = response.Value;

                // Content filter retry: if blocked, retry once before giving up
                if (ContentFilterHelper.IsContentFilterBlocked(completion))
                {
                    LogContentFilterBlocked(_logger, loopRound);
                    llmSw.Restart();
                    response = await chatClient.CompleteChatAsync(messages, options, linkedToken);
                    llmSw.Stop();
                    completion = response.Value;

                    if (ContentFilterHelper.IsContentFilterBlocked(completion))
                    {
                        LogContentFilterBlockedFinal(_logger, loopRound);
                        return AgentLoopResult.Partial(
                            "Response blocked by content filter after retry",
                            toolCallCount,
                            trace,
                            totalInputTokens, totalOutputTokens, totalTotalTokens,
                            llmTraces);
                    }
                }

                // Accumulate token usage
                var roundInputTokens = 0;
                var roundOutputTokens = 0;
                var roundTotalTokens = 0;
                if (completion.Usage is not null)
                {
                    roundInputTokens = completion.Usage.InputTokenCount;
                    roundOutputTokens = completion.Usage.OutputTokenCount;
                    roundTotalTokens = completion.Usage.TotalTokenCount;
                    totalInputTokens += roundInputTokens;
                    totalOutputTokens += roundOutputTokens;
                    totalTotalTokens += roundTotalTokens;
                }

                // Capture LLM trace for this round
                var responseText = completion.Content.Count > 0 ? completion.Content[0].Text : null;
                llmTraces.Add(new LlmCallTrace(
                    PromptText: SerializeMessagesForTrace(messages),
                    ResponseText: responseText,
                    ModelUsed: completion.Model ?? string.Empty,
                    LoopRound: loopRound,
                    InputTokens: roundInputTokens,
                    OutputTokens: roundOutputTokens,
                    TotalTokens: roundTotalTokens,
                    DurationMs: llmSw.ElapsedMilliseconds));

                // Add assistant message to conversation
                messages.Add(new AssistantChatMessage(completion));

                // Check if model wants to call tools
                if (completion.ToolCalls?.Count > 0)
                {
                    toolCallCount += completion.ToolCalls.Count;

                    LogAgentToolCalls(_logger, completion.ToolCalls.Count, toolCallCount);

                    // Execute tools in parallel with timing
                    var tasks = completion.ToolCalls.Select(tc => ExecuteToolCallAsync(toolArray, tc, loopRound, linkedToken));
                    var results = await Task.WhenAll(tasks);

                    // Record trace entries and add tool results to conversation
                    foreach (var (id, result, toolName, round, durationMs, inputJson) in results)
                    {
                        var outputJson = result.Data?.ToString();
                        trace.Add(new ToolCallEntry(toolName, result.Success, round, durationMs, inputJson, outputJson));
                        messages.Add(new ToolChatMessage(id, outputJson ?? string.Empty));
                    }

                    loopRound++;
                    continue;
                }

                // No tool calls = agent is done
                if (completion.FinishReason == ChatFinishReason.Stop)
                {
                    var content = completion.Content.Count > 0 ? completion.Content[0].Text : "";
                    return AgentLoopResult.Complete(content, toolCallCount, trace,
                        totalInputTokens, totalOutputTokens, totalTotalTokens,
                        llmTraces);
                }

                // Unexpected finish reason
                LogUnexpectedFinishReason(_logger, completion.FinishReason);
                return AgentLoopResult.Partial(
                    $"Unexpected completion: {completion.FinishReason}",
                    toolCallCount,
                    trace,
                    totalInputTokens, totalOutputTokens, totalTotalTokens,
                    llmTraces);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LogLoopTimeout(_logger, LoopTimeout.TotalMinutes, toolCallCount);
            return AgentLoopResult.Partial(
                $"Agent loop timed out after {LoopTimeout.TotalMinutes} minutes",
                toolCallCount,
                trace,
                totalInputTokens, totalOutputTokens, totalTotalTokens,
                llmTraces);
        }
    }

    private async Task<(string Id, ToolResult Result, string ToolName, int LoopRound, long DurationMs, string? InputJson)> ExecuteToolCallAsync(
        IEnumerable<IAgentTool> tools,
        ChatToolCall toolCall,
        int loopRound,
        CancellationToken ct)
    {
        var inputJson = toolCall.FunctionArguments.ToString();
        var tool = tools.FirstOrDefault(t => t.Name == toolCall.FunctionName);
        if (tool is null)
        {
            LogUnknownToolRequested(_logger, toolCall.FunctionName);
            return (toolCall.Id, ToolResult.Error($"Unknown tool: {toolCall.FunctionName}"), toolCall.FunctionName, loopRound, 0, inputJson);
        }

        LogExecutingTool(_logger, toolCall.FunctionName);
        var sw = Stopwatch.StartNew();
        var result = await tool.ExecuteAsync(toolCall.FunctionArguments, ct);
        sw.Stop();
        return (toolCall.Id, result, toolCall.FunctionName, loopRound, sw.ElapsedMilliseconds, inputJson);
    }

    private static string SerializeMessagesForTrace(List<ChatMessage> messages)
    {
        var parts = new List<string>();
        foreach (var msg in messages)
        {
            switch (msg)
            {
                case SystemChatMessage sys:
                    parts.Add($"[SYSTEM]\n{string.Join("\n", sys.Content.Where(p => p.Text is not null).Select(p => p.Text))}");
                    break;
                case UserChatMessage usr:
                    parts.Add($"[USER]\n{string.Join("\n", usr.Content.Where(p => p.Text is not null).Select(p => p.Text))}");
                    break;
                case AssistantChatMessage asst:
                    var text = string.Join("\n", asst.Content.Where(p => p.Text is not null).Select(p => p.Text));
                    var toolCallNames = asst.ToolCalls.Count > 0
                        ? $"\n[Tool Calls: {string.Join(", ", asst.ToolCalls.Select(tc => tc.FunctionName))}]"
                        : string.Empty;
                    parts.Add($"[ASSISTANT]\n{text}{toolCallNames}");
                    break;
                case ToolChatMessage tool:
                    parts.Add($"[TOOL]\n{string.Join("\n", tool.Content.Where(p => p.Text is not null).Select(p => p.Text))}");
                    break;
            }
        }
        return string.Join("\n---\n", parts);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent hit tool call limit of {Limit}")]
    private static partial void LogToolCallLimitHit(ILogger logger, int limit);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agent requested {Count} tool calls (total: {Total})")]
    private static partial void LogAgentToolCalls(ILogger logger, int count, int total);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown tool requested: {Name}")]
    private static partial void LogUnknownToolRequested(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing tool {Name}")]
    private static partial void LogExecutingTool(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unexpected finish reason: {Reason}")]
    private static partial void LogUnexpectedFinishReason(ILogger logger, ChatFinishReason reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent loop timed out after {Minutes} minutes with {ToolCalls} tool calls completed")]
    private static partial void LogLoopTimeout(ILogger logger, double minutes, int toolCalls);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content filter blocked response at loop round {Round}, retrying")]
    private static partial void LogContentFilterBlocked(ILogger logger, int round);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content filter blocked response at loop round {Round} after retry")]
    private static partial void LogContentFilterBlockedFinal(ILogger logger, int round);
}
