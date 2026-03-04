using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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
        IChatClient chatClient,
        List<ChatMessage> messages,
        CancellationToken ct = default)
    {
        return RunCoreAsync(chatClient, messages, _tools, null, null, ct);
    }

    public Task<AgentLoopResult> RunAsync(
        IChatClient chatClient,
        List<ChatMessage> messages,
        ChatResponseFormat? responseFormat,
        float? temperature = null,
        Func<LlmCallTrace, IReadOnlyList<ToolCallEntry>, Task>? onRoundComplete = null,
        CancellationToken ct = default)
    {
        return RunCoreAsync(chatClient, messages, _tools, responseFormat, temperature, ct, onRoundComplete);
    }

    public Task<AgentLoopResult> RunAsync(
        IChatClient chatClient,
        List<ChatMessage> messages,
        IEnumerable<IAgentTool> tools,
        float? temperature = null,
        CancellationToken ct = default)
    {
        return RunCoreAsync(chatClient, messages, tools, null, temperature, ct);
    }

#pragma warning disable S3776 // Cognitive complexity - agent loop requires sequential control flow
    private async Task<AgentLoopResult> RunCoreAsync(
        IChatClient chatClient,
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
        var toolList = toolArray.ToAITools().ToList();
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

                var options = new ChatOptions();
                if (responseFormat is not null)
                {
                    options.ResponseFormat = responseFormat;
                }
                if (temperature.HasValue)
                {
                    options.Temperature = temperature.Value;
                }
                options.Tools = toolList;

                var llmSw = Stopwatch.StartNew();
                var response = await chatClient.GetResponseAsync(messages, options, linkedToken);
                llmSw.Stop();

                // Content filter retry: if blocked, retry once before giving up
                if (ContentFilterHelper.IsContentFilterBlocked(response))
                {
                    LogContentFilterBlocked(_logger, loopRound, response.FinishReason?.ToString() ?? "unknown", response.Messages.Count);
                    llmSw.Restart();
                    response = await chatClient.GetResponseAsync(messages, options, linkedToken);
                    llmSw.Stop();

                    if (ContentFilterHelper.IsContentFilterBlocked(response))
                    {
                        LogContentFilterBlockedFinal(_logger, loopRound, response.FinishReason?.ToString() ?? "unknown", response.Messages.Count);
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
                if (response.Usage is not null)
                {
                    roundInputTokens = (int)(response.Usage.InputTokenCount ?? 0);
                    roundOutputTokens = (int)(response.Usage.OutputTokenCount ?? 0);
                    roundTotalTokens = (int)(response.Usage.TotalTokenCount ?? 0);
                    totalInputTokens += roundInputTokens;
                    totalOutputTokens += roundOutputTokens;
                    totalTotalTokens += roundTotalTokens;
                }

                // Capture LLM trace for this round with delta segments
                var responseText = response.Text;
                var deltaSegmentsJson = SerializeDeltaSegments(messages, promptStartForDelta);
                llmTraces.Add(new LlmCallTrace(
                    PromptText: null,
                    PromptSegmentsJson: deltaSegmentsJson,
                    ResponseText: responseText,
                    ModelUsed: response.ModelId ?? string.Empty,
                    LoopRound: loopRound,
                    InputTokens: roundInputTokens,
                    OutputTokens: roundOutputTokens,
                    TotalTokens: roundTotalTokens,
                    DurationMs: llmSw.ElapsedMilliseconds));

                // Add response messages to conversation history
                messages.AddRange(response.Messages);
                promptStartForDelta = messages.Count;

                // Check if model wants to call tools
                var functionCalls = response.Messages
                    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                    .ToList();

                if (functionCalls.Count > 0)
                {
                    toolCallCount += functionCalls.Count;

                    LogAgentToolCalls(_logger, functionCalls.Count, toolCallCount);

                    // Execute tools sequentially — scoped tools share a DbContext which is not thread-safe
                    var results = new List<(string CallId, ToolResult Result, string ToolName, int LoopRound, long DurationMs, string? InputJson)>();
                    foreach (var fc in functionCalls)
                        results.Add(await ExecuteToolCallAsync(toolArray, fc, loopRound, linkedToken));

                    // Record trace entries and add tool results to conversation
                    var roundToolCalls = new List<ToolCallEntry>();
                    foreach (var (callId, result, toolName, round, durationMs, inputJson) in results)
                    {
                        var outputJson = result.Data?.ToString();
                        var entry = new ToolCallEntry(toolName, result.Success, round, durationMs, inputJson, outputJson);
                        trace.Add(entry);
                        roundToolCalls.Add(entry);
                        messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, outputJson ?? string.Empty)]));
                    }

                    if (onRoundComplete != null)
                        await onRoundComplete(llmTraces[^1], roundToolCalls);

                    loopRound++;
                    continue;
                }

                // No tool calls = agent is done
                if (response.FinishReason == ChatFinishReason.Stop)
                {
                    if (onRoundComplete != null)
                        await onRoundComplete(llmTraces[^1], Array.Empty<ToolCallEntry>());

                    var content = response.Text ?? "";
                    return AgentLoopResult.Complete(content, toolCallCount, trace,
                        totalInputTokens, totalOutputTokens, totalTotalTokens,
                        llmTraces);
                }

                // Unexpected finish reason — still fire callback so trace is saved incrementally
                if (onRoundComplete != null)
                    await onRoundComplete(llmTraces[^1], Array.Empty<ToolCallEntry>());

                LogUnexpectedFinishReason(_logger, response.FinishReason?.ToString());
                return AgentLoopResult.Partial(
                    $"Unexpected completion: {response.FinishReason}",
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

    private async Task<(string CallId, ToolResult Result, string ToolName, int LoopRound, long DurationMs, string? InputJson)> ExecuteToolCallAsync(
        IEnumerable<IAgentTool> tools,
        FunctionCallContent functionCall,
        int loopRound,
        CancellationToken ct)
    {
        var inputJson = JsonSerializer.Serialize(functionCall.Arguments);
        var tool = tools.FirstOrDefault(t => t.Name == functionCall.Name);
        if (tool is null)
        {
            LogUnknownToolRequested(_logger, functionCall.Name);
            return (functionCall.CallId, ToolResult.Error($"Unknown tool: {functionCall.Name}"), functionCall.Name, loopRound, 0, inputJson);
        }

        LogExecutingTool(_logger, functionCall.Name);
        var sw = Stopwatch.StartNew();
        var result = await tool.ExecuteAsync(BinaryData.FromObjectAsJson(functionCall.Arguments), ct);
        sw.Stop();
        return (functionCall.CallId, result, functionCall.Name, loopRound, sw.ElapsedMilliseconds, inputJson);
    }

    internal static string SerializeDeltaSegments(List<ChatMessage> messages, int startIndex)
    {
        var segments = new List<object>();
        for (var i = startIndex; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (msg.Role == ChatRole.System)
            {
                segments.Add(new { role = "system", content = msg.Text ?? string.Empty });
            }
            else if (msg.Role == ChatRole.User)
            {
                segments.Add(new { role = "user", content = msg.Text ?? string.Empty });
            }
            else if (msg.Role == ChatRole.Assistant)
            {
                var text = msg.Text ?? string.Empty;
                var fcContents = msg.Contents.OfType<FunctionCallContent>().ToList();
                var toolCallNames = fcContents.Count > 0
                    ? string.Join(", ", fcContents.Select(fc => fc.Name))
                    : null;
                segments.Add(new { role = "assistant", content = text, toolCalls = toolCallNames });
            }
            else if (msg.Role == ChatRole.Tool)
            {
                var resultTexts = msg.Contents
                    .OfType<FunctionResultContent>()
                    .Select(fr => fr.Result?.ToString() ?? string.Empty);
                segments.Add(new { role = "tool", content = string.Join("\n", resultTexts) });
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
    private static partial void LogUnexpectedFinishReason(ILogger logger, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent loop timed out after {Minutes} minutes with {ToolCalls} tool calls completed")]
    private static partial void LogLoopTimeout(ILogger logger, double minutes, int toolCalls);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content filter blocked response at loop round {Round} (FinishReason={FinishReason}, MessageCount={MessageCount}), retrying")]
    private static partial void LogContentFilterBlocked(ILogger logger, int round, string finishReason, int messageCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content filter blocked response at loop round {Round} after retry (FinishReason={FinishReason}, MessageCount={MessageCount})")]
    private static partial void LogContentFilterBlockedFinal(ILogger logger, int round, string finishReason, int messageCount);
}
