using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Core.Entities;

public class ExtractionResult
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public string SchemaVersion { get; set; } = "1.0.0";
    public string ModelUsed { get; set; } = string.Empty;
    public double OverallConfidence { get; set; }
    public bool RequiresReview { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.NotFlagged;
    public List<string> ReviewReasons { get; set; } = new();
    public DateTime ExtractedAt { get; set; }
    public ClinicalExtraction Data { get; set; } = new();

    /// <summary>
    /// Session summary stored as JSON string.
    /// </summary>
    public string? SummaryJson { get; set; }

    /// <summary>
    /// Number of attempts used by criteria/reasoning validation for risk re-extraction.
    /// </summary>
    public int CriteriaValidationAttemptsUsed { get; set; } = 1;

    /// <summary>
    /// Whether homicidal guardrail logic changed the final value.
    /// </summary>
    public bool HomicidalGuardrailApplied { get; set; }

    /// <summary>
    /// Optional explanation for homicidal guardrail application.
    /// </summary>
    public string? HomicidalGuardrailReason { get; set; }

    /// <summary>
    /// Whether self-harm guardrail logic changed the final value.
    /// </summary>
    public bool SelfHarmGuardrailApplied { get; set; }

    /// <summary>
    /// Optional explanation for self-harm guardrail application.
    /// </summary>
    public string? SelfHarmGuardrailReason { get; set; }

    /// <summary>
    /// Per-field risk decisions stored as JSON string (trimmed diagnostics payload).
    /// </summary>
    public string? RiskDecisionsJson { get; set; }

    public ICollection<SupervisorReview> Reviews { get; set; } = new List<SupervisorReview>();
}
