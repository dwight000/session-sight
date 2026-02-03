namespace SessionSight.Agents.Models;

/// <summary>
/// Summary of a single therapy session, auto-generated after extraction.
/// </summary>
public class SessionSummary
{
    /// <summary>
    /// The session ID this summary belongs to.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// One-line summary of the session (2-3 sentences max).
    /// </summary>
    public string OneLiner { get; set; } = string.Empty;

    /// <summary>
    /// Key clinical points from the session.
    /// </summary>
    public string KeyPoints { get; set; } = string.Empty;

    /// <summary>
    /// Interventions or techniques used during the session.
    /// </summary>
    public List<string> InterventionsUsed { get; set; } = new();

    /// <summary>
    /// Recommended focus areas for the next session.
    /// </summary>
    public string NextSessionFocus { get; set; } = string.Empty;

    /// <summary>
    /// Summary of risk-related flags, if any.
    /// </summary>
    public RiskSummary? RiskFlags { get; set; }

    /// <summary>
    /// The model used to generate this summary.
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// When this summary was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Condensed risk information for the session summary.
/// </summary>
public class RiskSummary
{
    /// <summary>
    /// Overall risk level determined for the session.
    /// </summary>
    public string RiskLevel { get; set; } = "Low";

    /// <summary>
    /// List of specific risk flags identified.
    /// </summary>
    public List<string> Flags { get; set; } = new();

    /// <summary>
    /// Whether this session requires clinical review.
    /// </summary>
    public bool RequiresReview { get; set; }
}
