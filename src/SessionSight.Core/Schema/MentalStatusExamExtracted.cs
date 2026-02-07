using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class MentalStatusExamExtracted
{
    public ExtractedField<Appearance> Appearance { get; set; } = new();
    public ExtractedField<BehaviorType> Behavior { get; set; } = new();
    public ExtractedField<SpeechType> Speech { get; set; } = new();
    public ExtractedField<ThoughtProcess> ThoughtProcess { get; set; } = new();
    public ExtractedField<List<string>> ThoughtContent { get; set; } = new();
    public ExtractedField<List<string>> Perception { get; set; } = new();
    public ExtractedField<Cognition> Cognition { get; set; } = new();
    public ExtractedField<InsightLevel> Insight { get; set; } = new();
    public ExtractedField<JudgmentLevel> Judgment { get; set; } = new();
}
