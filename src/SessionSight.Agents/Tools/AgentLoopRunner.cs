using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Runs an agent loop that allows the LLM to call tools until completion.
/// </summary>
public partial class AgentLoopRunner
{
    public const int MaxToolCalls = 15;

    private readonly IEnumerable<IAgentTool> _tools;
    private readonly ILogger<AgentLoopRunner> _logger;

    public AgentLoopRunner(IEnumerable<IAgentTool> tools, ILogger<AgentLoopRunner> logger)
    {
        _tools = tools;
        _logger = logger;
    }

    public async Task<AgentLoopResult> RunAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        CancellationToken ct = default)
    {
        var toolCallCount = 0;
        var toolList = _tools.ToChatTools().ToList();

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
            foreach (var tool in toolList)
            {
                options.Tools.Add(tool);
            }

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            var completion = response.Value;

            // Add assistant message to conversation
            messages.Add(new AssistantChatMessage(completion));

            // Check if model wants to call tools
            if (completion.ToolCalls?.Count > 0)
            {
                toolCallCount += completion.ToolCalls.Count;

                LogAgentToolCalls(_logger, completion.ToolCalls.Count, toolCallCount);

                // Execute tools in parallel
                var tasks = completion.ToolCalls.Select(async tc =>
                {
                    var tool = _tools.FirstOrDefault(t => t.Name == tc.FunctionName);
                    if (tool is null)
                    {
                        LogUnknownToolRequested(_logger, tc.FunctionName);
                        return (tc.Id, ToolResult.Error($"Unknown tool: {tc.FunctionName}"));
                    }

                    LogExecutingTool(_logger, tc.FunctionName);
                    return (tc.Id, await tool.ExecuteAsync(tc.FunctionArguments, ct));
                });

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
}
