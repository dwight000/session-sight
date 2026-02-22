namespace SessionSight.Agents.Tools;

/// <summary>
/// A single tool invocation recorded during an agent loop.
/// </summary>
public sealed record ToolCallEntry(string ToolName, bool Succeeded, int LoopRound = 0, long DurationMs = 0);

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
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens { get; init; }

    public static AgentLoopResult Complete(string content, int toolCallCount = 0,
        IReadOnlyList<ToolCallEntry>? toolCallTrace = null,
        int inputTokens = 0, int outputTokens = 0, int totalTokens = 0) => new()
        {
            IsComplete = true,
            Content = content,
            ToolCallCount = toolCallCount,
            ToolCallTrace = toolCallTrace ?? [],
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens
        };

    public static AgentLoopResult Partial(string reason, int toolCallCount = 0,
        IReadOnlyList<ToolCallEntry>? toolCallTrace = null,
        int inputTokens = 0, int outputTokens = 0, int totalTokens = 0) => new()
        {
            IsComplete = false,
            PartialReason = reason,
            ToolCallCount = toolCallCount,
            ToolCallTrace = toolCallTrace ?? [],
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens
        };
}
