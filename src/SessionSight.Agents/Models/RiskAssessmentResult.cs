using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Models;

/// <summary>
/// Result from the Risk Assessor Agent's validation of risk-related extractions.
/// </summary>
public class RiskAssessmentResult
{
    /// <summary>
    /// Original extraction from Clinical Extractor Agent.
    /// </summary>
    public RiskAssessmentExtracted OriginalExtraction { get; set; } = new();

    /// <summary>
    /// Re-extracted values from focused safety prompt.
    /// </summary>
    public RiskAssessmentExtracted ValidatedExtraction { get; set; } = new();

    /// <summary>
    /// Final merged extraction (conservative merge of original and validated).
    /// </summary>
    public RiskAssessmentExtracted FinalExtraction { get; set; } = new();

    /// <summary>
    /// Whether human review is required.
    /// </summary>
    public bool RequiresReview { get; set; }

    /// <summary>
    /// Reasons why review is required (empty if no review needed).
    /// </summary>
    public List<string> ReviewReasons { get; set; } = new();

    /// <summary>
    /// Fields where original and re-extraction disagreed.
    /// </summary>
    public List<RiskDiscrepancy> Discrepancies { get; set; } = new();

    /// <summary>
    /// Danger keywords found in the note.
    /// </summary>
    public List<string> KeywordMatches { get; set; } = new();

    /// <summary>
    /// Overall risk level determination.
    /// </summary>
    public RiskLevelOverall DeterminedRiskLevel { get; set; }

    /// <summary>
    /// Model used for re-extraction.
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Structured diagnostic details for risk-field decisions.
    /// </summary>
    public RiskDiagnostics Diagnostics { get; set; } = new();
}
