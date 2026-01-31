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
}
