namespace SessionSight.Agents.Models;

/// <summary>
/// Cross-session summary for a patient, synthesizing treatment history.
/// </summary>
public class PatientSummary
{
    /// <summary>
    /// The patient ID this summary belongs to.
    /// </summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Date range covered by this summary.
    /// </summary>
    public DateRange Period { get; set; } = new();

    /// <summary>
    /// Number of sessions included in this summary.
    /// </summary>
    public int SessionCount { get; set; }

    /// <summary>
    /// Narrative description of patient's progress over the period.
    /// </summary>
    public string ProgressNarrative { get; set; } = string.Empty;

    /// <summary>
    /// Overall mood trend observed across sessions.
    /// </summary>
    public MoodTrend MoodTrend { get; set; } = MoodTrend.InsufficientData;

    /// <summary>
    /// Key themes that emerged across sessions.
    /// </summary>
    public List<string> RecurringThemes { get; set; } = new();

    /// <summary>
    /// Treatment goals and their current status.
    /// </summary>
    public List<GoalProgress> GoalProgress { get; set; } = new();

    /// <summary>
    /// Interventions that have shown effectiveness.
    /// </summary>
    public List<string> EffectiveInterventions { get; set; } = new();

    /// <summary>
    /// Recommended focus areas for future sessions.
    /// </summary>
    public string RecommendedFocus { get; set; } = string.Empty;

    /// <summary>
    /// Risk trend across the period.
    /// </summary>
    public string RiskTrendSummary { get; set; } = string.Empty;

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
/// Date range for summary periods.
/// </summary>
public class DateRange
{
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
}

/// <summary>
/// Mood trend observed across sessions.
/// </summary>
public enum MoodTrend
{
    Improving,
    Stable,
    Declining,
    Variable,
    InsufficientData
}

/// <summary>
/// Progress toward a treatment goal.
/// </summary>
public class GoalProgress
{
    /// <summary>
    /// Description of the treatment goal.
    /// </summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>
    /// Current status of progress toward this goal.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
