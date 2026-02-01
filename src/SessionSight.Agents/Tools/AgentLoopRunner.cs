using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Runs an agent loop that allows the LLM to call tools until completion.
/// </summary>
public class AgentLoopRunner
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
                _logger.LogWarning("Agent hit tool call limit of {Limit}", MaxToolCalls);
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

                _logger.LogDebug("Agent requested {Count} tool calls (total: {Total})",
                    completion.ToolCalls.Count, toolCallCount);

                // Execute tools in parallel
                var tasks = completion.ToolCalls.Select(async tc =>
                {
                    var tool = _tools.FirstOrDefault(t => t.Name == tc.FunctionName);
                    if (tool is null)
                    {
                        _logger.LogWarning("Unknown tool requested: {Name}", tc.FunctionName);
                        return (tc.Id, ToolResult.Error($"Unknown tool: {tc.FunctionName}"));
                    }

                    _logger.LogDebug("Executing tool {Name}", tc.FunctionName);
                    return (tc.Id, await tool.ExecuteAsync(tc.FunctionArguments, ct));
                });

                var results = await Task.WhenAll(tasks);

                // Add tool results to conversation
                foreach (var (id, result) in results)
                {
                    messages.Add(new ToolChatMessage(id, result.Data.ToString()));
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
            _logger.LogWarning("Unexpected finish reason: {Reason}", completion.FinishReason);
            return AgentLoopResult.Partial(
                $"Unexpected completion: {completion.FinishReason}",
                toolCallCount);
        }
    }
}
