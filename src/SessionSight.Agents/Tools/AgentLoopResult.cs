namespace SessionSight.Agents.Tools;

/// <summary>
/// A single tool invocation recorded during an agent loop.
/// </summary>
public sealed record ToolCallEntry(string ToolName, bool Succeeded);

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
    public IReadOnlyList<ToolCallEntry> ToolCallTrace { get; init; } = [];

    public static AgentLoopResult Complete(string content, int toolCallCount = 0,
        IReadOnlyList<ToolCallEntry>? toolCallTrace = null) => new()
    {
        IsComplete = true,
        Content = content,
        ToolCallCount = toolCallCount,
        ToolCallTrace = toolCallTrace ?? []
    };

    public static AgentLoopResult Partial(string reason, int toolCallCount = 0,
        IReadOnlyList<ToolCallEntry>? toolCallTrace = null) => new()
    {
        IsComplete = false,
        PartialReason = reason,
        ToolCallCount = toolCallCount,
        ToolCallTrace = toolCallTrace ?? []
    };
}
