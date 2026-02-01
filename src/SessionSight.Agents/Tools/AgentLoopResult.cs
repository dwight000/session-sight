namespace SessionSight.Agents.Tools;

/// <summary>
/// Result of an agent loop execution.
/// </summary>
public class AgentLoopResult
{
    public bool IsComplete { get; private init; }
    public bool IsPartial => !IsComplete;
    public string? Content { get; private init; }
    public string? PartialReason { get; private init; }
    public int ToolCallCount { get; init; }

    public static AgentLoopResult Complete(string content, int toolCallCount = 0) => new()
    {
        IsComplete = true,
        Content = content,
        ToolCallCount = toolCallCount
    };

    public static AgentLoopResult Partial(string reason, int toolCallCount = 0) => new()
    {
        IsComplete = false,
        PartialReason = reason,
        ToolCallCount = toolCallCount
    };
}
