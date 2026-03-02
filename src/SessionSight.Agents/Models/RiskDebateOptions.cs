namespace SessionSight.Agents.Models;

public enum RiskDebateTriggerMode
{
    Off,
    Always,
    Flagged,
    Borderline
}

public class RiskDebateOptions
{
    public const string SectionName = "RiskDebate";

    public bool Enabled { get; set; }
    public RiskDebateTriggerMode TriggerMode { get; set; } = RiskDebateTriggerMode.Borderline;
    public double LowConfidenceThreshold { get; set; } = 0.3;
    public double HighConfidenceThreshold { get; set; } = 0.7;
}
