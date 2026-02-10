using SessionSight.Agents.Models;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Orchestration;

/// <summary>
/// Result from the extraction orchestration pipeline.
/// </summary>
public class OrchestrationResult
{
    /// <summary>
    /// Whether the extraction completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The session ID that was processed.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// The ID of the saved extraction result (if successful).
    /// </summary>
    public Guid ExtractionId { get; set; }

    /// <summary>
    /// Whether the extraction requires human review.
    /// True if low confidence or risk discrepancies were found.
    /// </summary>
    public bool RequiresReview { get; set; }

    /// <summary>
    /// Error message if the extraction failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Models used during extraction (for auditing).
    /// </summary>
    public List<string> ModelsUsed { get; set; } = new();

    /// <summary>
    /// Time taken for the extraction in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Number of tool calls made by the agent during extraction.
    /// </summary>
    public int ToolCallCount { get; set; }

    /// <summary>
    /// Risk stage outputs captured during orchestration for diagnostics/testing.
    /// </summary>
    public RiskStageOutputs? RiskStageOutputs { get; set; }

    /// <summary>
    /// Structured risk diagnostics from risk assessor merge/guardrail decisions.
    /// </summary>
    public RiskDiagnostics? RiskDiagnostics { get; set; }
}

/// <summary>
/// Captures risk extraction snapshots for each stage of the risk pipeline.
/// </summary>
public sealed class RiskStageOutputs
{
    public RiskAssessmentExtracted ClinicalExtractor { get; set; } = new();

    public RiskAssessmentExtracted RiskReextracted { get; set; } = new();

    public RiskAssessmentExtracted RiskFinal { get; set; } = new();
}
