using Microsoft.Extensions.Logging;
using OpenAI.Chat;

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

    private async Task<AgentLoopResult> RunCoreAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        IEnumerable<IAgentTool> tools,
        ChatResponseFormat? responseFormat,
        float? temperature,
        CancellationToken ct)
    {
        var toolCallCount = 0;
        var toolArray = tools as IAgentTool[] ?? tools.ToArray();
        var toolList = toolArray.ToChatTools().ToList();

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
                        toolCallCount);
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

                var response = await chatClient.CompleteChatAsync(messages, options, linkedToken);
                var completion = response.Value;

                // Add assistant message to conversation
                messages.Add(new AssistantChatMessage(completion));

                // Check if model wants to call tools
                if (completion.ToolCalls?.Count > 0)
                {
                    toolCallCount += completion.ToolCalls.Count;

                    LogAgentToolCalls(_logger, completion.ToolCalls.Count, toolCallCount);

                    // Execute tools in parallel
                    var tasks = completion.ToolCalls.Select(tc => ExecuteToolCallAsync(toolArray, tc, linkedToken));
                    var results = await Task.WhenAll(tasks);

                    // Add tool results to conversation
                    foreach (var (id, result) in results)
                    {
                        messages.Add(new ToolChatMessage(id, result.Data?.ToString() ?? string.Empty));
                    }

                    continue;
                }

                // No tool calls = agent is done
                if (completion.FinishReason == ChatFinishReason.Stop)
                {
                    var content = completion.Content.Count > 0 ? completion.Content[0].Text : "";
                    return AgentLoopResult.Complete(content, toolCallCount);
                }

                // Unexpected finish reason
                LogUnexpectedFinishReason(_logger, completion.FinishReason);
                return AgentLoopResult.Partial(
                    $"Unexpected completion: {completion.FinishReason}",
                    toolCallCount);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LogLoopTimeout(_logger, LoopTimeout.TotalMinutes, toolCallCount);
            return AgentLoopResult.Partial(
                $"Agent loop timed out after {LoopTimeout.TotalMinutes} minutes",
                toolCallCount);
        }
    }

    private async Task<(string Id, ToolResult Result)> ExecuteToolCallAsync(
        IEnumerable<IAgentTool> tools,
        ChatToolCall toolCall,
        CancellationToken ct)
    {
        var tool = tools.FirstOrDefault(t => t.Name == toolCall.FunctionName);
        if (tool is null)
        {
            LogUnknownToolRequested(_logger, toolCall.FunctionName);
            return (toolCall.Id, ToolResult.Error($"Unknown tool: {toolCall.FunctionName}"));
        }

        LogExecutingTool(_logger, toolCall.FunctionName);
        return (toolCall.Id, await tool.ExecuteAsync(toolCall.FunctionArguments, ct));
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
}
