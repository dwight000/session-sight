namespace SessionSight.Agents.Models;

/// <summary>
/// Practice-level summary aggregating metrics across patients and sessions.
/// </summary>
public class PracticeSummary
{
    /// <summary>
    /// Date range covered by this summary.
    /// </summary>
    public DateRange Period { get; set; } = new();

    /// <summary>
    /// Total number of unique patients seen in the period.
    /// </summary>
    public int TotalPatients { get; set; }

    /// <summary>
    /// Total number of sessions conducted in the period.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Number of sessions that required clinical review.
    /// </summary>
    public int SessionsRequiringReview { get; set; }

    /// <summary>
    /// Number of patients with elevated risk flags.
    /// </summary>
    public int FlaggedPatientCount { get; set; }

    /// <summary>
    /// Summary of patients with elevated risk.
    /// </summary>
    public List<FlaggedPatientSummary> FlaggedPatients { get; set; } = new();

    /// <summary>
    /// Breakdown of sessions by risk level.
    /// </summary>
    public RiskLevelBreakdown RiskDistribution { get; set; } = new();

    /// <summary>
    /// Average sessions per patient in the period.
    /// </summary>
    public double AverageSessionsPerPatient { get; set; }

    /// <summary>
    /// Most common interventions used across the practice.
    /// </summary>
    public List<InterventionFrequency> TopInterventions { get; set; } = new();

    /// <summary>
    /// When this summary was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Summary of a patient with elevated risk flags.
/// </summary>
public class FlaggedPatientSummary
{
    /// <summary>
    /// Patient identifier.
    /// </summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Patient's display name or identifier.
    /// </summary>
    public string PatientIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Highest risk level observed in the period.
    /// </summary>
    public string HighestRiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Number of sessions with elevated risk.
    /// </summary>
    public int FlaggedSessionCount { get; set; }

    /// <summary>
    /// Most recent session date.
    /// </summary>
    public DateOnly LastSessionDate { get; set; }

    /// <summary>
    /// Brief reason for flagging.
    /// </summary>
    public string FlagReason { get; set; } = string.Empty;
}

/// <summary>
/// Distribution of sessions by risk level.
/// </summary>
public class RiskLevelBreakdown
{
    public int Low { get; set; }
    public int Moderate { get; set; }
    public int High { get; set; }
    public int Imminent { get; set; }
}

/// <summary>
/// Intervention usage frequency.
/// </summary>
public class InterventionFrequency
{
    /// <summary>
    /// Name of the intervention.
    /// </summary>
    public string Intervention { get; set; } = string.Empty;

    /// <summary>
    /// Number of times used in the period.
    /// </summary>
    public int Count { get; set; }
}
