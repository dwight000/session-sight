namespace SessionSight.Agents.Models;

/// <summary>
/// Configuration options for the Risk Assessor Agent.
/// </summary>
public class RiskAssessorOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "RiskAssessor";

    /// <summary>
    /// Minimum confidence threshold for risk fields (per ADR-004).
    /// </summary>
    public double RiskConfidenceThreshold { get; set; } = 0.9;

    /// <summary>
    /// Whether to always re-extract (true = safety-first).
    /// </summary>
    public bool AlwaysReExtract { get; set; } = true;

    /// <summary>
    /// Whether to check for danger keywords.
    /// </summary>
    public bool EnableKeywordSafetyNet { get; set; } = true;

    /// <summary>
    /// Whether to use conservative merge (more severe value wins).
    /// </summary>
    public bool UseConservativeMerge { get; set; } = true;

    /// <summary>
    /// Whether risk re-extraction must include non-empty criteria_used values for all required risk fields.
    /// </summary>
    public bool RequireCriteriaUsed { get; set; } = true;

    /// <summary>
    /// Max attempts for risk re-extraction when criteria_used validation fails.
    /// Includes the initial attempt.
    /// </summary>
    public int CriteriaValidationAttempts { get; set; } = 2;
}
