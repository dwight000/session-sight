namespace SessionSight.Core.Entities;

public class ExtractionToolCall
{
    public Guid Id { get; set; }
    public Guid StepId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public int LoopRound { get; set; }
    public bool Succeeded { get; set; }
    public long DurationMs { get; set; }
    public DateTime CalledAt { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }

    public ExtractionStep Step { get; set; } = null!;
}
