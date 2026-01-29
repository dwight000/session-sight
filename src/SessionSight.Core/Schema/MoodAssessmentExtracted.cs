using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class MoodAssessmentExtracted
{
    public ExtractedField<int> SelfReportedMood { get; set; } = new();
    public ExtractedField<ObservedAffect> ObservedAffect { get; set; } = new();
    public ExtractedField<AffectCongruence> AffectCongruence { get; set; } = new();
    public ExtractedField<MoodChange> MoodChangeFromLast { get; set; } = new();
    public ExtractedField<MoodVariability> MoodVariability { get; set; } = new();
    public ExtractedField<EnergyLevel> EnergyLevel { get; set; } = new();
    public ExtractedField<List<string>> EmotionalThemes { get; set; } = new();
}
