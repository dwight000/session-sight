using System.Diagnostics;
using System.Text.Json;
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
        Func<LlmCallTrace, IReadOnlyList<ToolCallEntry>, Task>? onRoundComplete = null,
        CancellationToken ct = default)
    {
        return RunCoreAsync(chatClient, messages, _tools, responseFormat, temperature, ct, onRoundComplete);
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
        CancellationToken ct,
        Func<LlmCallTrace, IReadOnlyList<ToolCallEntry>, Task>? onRoundComplete = null)
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
        var promptStartForDelta = 0;

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
                    LogContentFilterBlocked(_logger, loopRound, completion.FinishReason.ToString(), completion.Content.Count);
                    llmSw.Restart();
                    response = await chatClient.CompleteChatAsync(messages, options, linkedToken);
                    llmSw.Stop();
                    completion = response.Value;

                    if (ContentFilterHelper.IsContentFilterBlocked(completion))
                    {
                        LogContentFilterBlockedFinal(_logger, loopRound, completion.FinishReason.ToString(), completion.Content.Count);
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

                // Capture LLM trace for this round with delta segments
                var responseText = completion.Content.Count > 0 ? completion.Content[0].Text : null;
                var deltaSegmentsJson = SerializeDeltaSegments(messages, promptStartForDelta);
                llmTraces.Add(new LlmCallTrace(
                    PromptText: null,
                    PromptSegmentsJson: deltaSegmentsJson,
                    ResponseText: responseText,
                    ModelUsed: completion.Model ?? string.Empty,
                    LoopRound: loopRound,
                    InputTokens: roundInputTokens,
                    OutputTokens: roundOutputTokens,
                    TotalTokens: roundTotalTokens,
                    DurationMs: llmSw.ElapsedMilliseconds));

                // Add assistant message to conversation
                messages.Add(new AssistantChatMessage(completion));
                promptStartForDelta = messages.Count;

                // Check if model wants to call tools
                if (completion.ToolCalls?.Count > 0)
                {
                    toolCallCount += completion.ToolCalls.Count;

                    LogAgentToolCalls(_logger, completion.ToolCalls.Count, toolCallCount);

                    // Execute tools in parallel with timing
                    var tasks = completion.ToolCalls.Select(tc => ExecuteToolCallAsync(toolArray, tc, loopRound, linkedToken));
                    var results = await Task.WhenAll(tasks);

                    // Record trace entries and add tool results to conversation
                    var roundToolCalls = new List<ToolCallEntry>();
                    foreach (var (id, result, toolName, round, durationMs, inputJson) in results)
                    {
                        var outputJson = result.Data?.ToString();
                        var entry = new ToolCallEntry(toolName, result.Success, round, durationMs, inputJson, outputJson);
                        trace.Add(entry);
                        roundToolCalls.Add(entry);
                        messages.Add(new ToolChatMessage(id, outputJson ?? string.Empty));
                    }

                    if (onRoundComplete != null)
                        await onRoundComplete(llmTraces[^1], roundToolCalls);

                    loopRound++;
                    continue;
                }

                // No tool calls = agent is done
                if (completion.FinishReason == ChatFinishReason.Stop)
                {
                    if (onRoundComplete != null)
                        await onRoundComplete(llmTraces[^1], Array.Empty<ToolCallEntry>());

                    var content = completion.Content.Count > 0 ? completion.Content[0].Text : "";
                    return AgentLoopResult.Complete(content, toolCallCount, trace,
                        totalInputTokens, totalOutputTokens, totalTotalTokens,
                        llmTraces);
                }

                // Unexpected finish reason — still fire callback so trace is saved incrementally
                if (onRoundComplete != null)
                    await onRoundComplete(llmTraces[^1], Array.Empty<ToolCallEntry>());

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

    internal static string SerializeDeltaSegments(List<ChatMessage> messages, int startIndex)
    {
        var segments = new List<object>();
        for (var i = startIndex; i < messages.Count; i++)
        {
            var msg = messages[i];
            switch (msg)
            {
                case SystemChatMessage sys:
                    segments.Add(new { role = "system", content = string.Join("\n", sys.Content.Where(p => p.Text is not null).Select(p => p.Text)) });
                    break;
                case UserChatMessage usr:
                    segments.Add(new { role = "user", content = string.Join("\n", usr.Content.Where(p => p.Text is not null).Select(p => p.Text)) });
                    break;
                case AssistantChatMessage asst:
                    var text = string.Join("\n", asst.Content.Where(p => p.Text is not null).Select(p => p.Text));
                    var toolCallNames = asst.ToolCalls.Count > 0
                        ? string.Join(", ", asst.ToolCalls.Select(tc => tc.FunctionName))
                        : null;
                    segments.Add(new { role = "assistant", content = text, toolCalls = toolCallNames });
                    break;
                case ToolChatMessage tool:
                    segments.Add(new { role = "tool", content = string.Join("\n", tool.Content.Where(p => p.Text is not null).Select(p => p.Text)) });
                    break;
            }
        }
        return JsonSerializer.Serialize(segments);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content filter blocked response at loop round {Round} (FinishReason={FinishReason}, ContentCount={ContentCount}), retrying")]
    private static partial void LogContentFilterBlocked(ILogger logger, int round, string finishReason, int contentCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content filter blocked response at loop round {Round} after retry (FinishReason={FinishReason}, ContentCount={ContentCount})")]
    private static partial void LogContentFilterBlockedFinal(ILogger logger, int round, string finishReason, int contentCount);
}
