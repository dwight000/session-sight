using SessionSight.Core.Enums;

namespace SessionSight.Core.Entities;

public class ExtractionStep
{
    public Guid Id { get; set; }
    public Guid ExtractionId { get; set; }
    public ExtractionStepName StepName { get; set; }
    public ExtractionStepStatus Status { get; set; }
    public int StepOrder { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public string? ResultSummaryJson { get; set; }
    public string? ErrorMessage { get; set; }

    public ExtractionResult Extraction { get; set; } = null!;
    public ICollection<ExtractionToolCall> ToolCalls { get; set; } = new List<ExtractionToolCall>();
    public ICollection<ExtractionLlmTrace> LlmTraces { get; set; } = new List<ExtractionLlmTrace>();
}
