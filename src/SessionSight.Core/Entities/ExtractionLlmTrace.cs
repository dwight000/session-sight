namespace SessionSight.Core.Entities;

public class ExtractionLlmTrace
{
    public Guid Id { get; set; }
    public Guid StepId { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public int LoopRound { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public long DurationMs { get; set; }
    public string? PromptText { get; set; }
    public string? PromptSegmentsJson { get; set; }
    public string? ResponseText { get; set; }
    public DateTime CalledAt { get; set; }

    public ExtractionStep Step { get; set; } = null!;
}
