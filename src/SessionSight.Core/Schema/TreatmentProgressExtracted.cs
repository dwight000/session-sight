using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class TreatmentProgressExtracted
{
    public ExtractedField<List<string>> TreatmentGoals { get; set; } = new();
    public ExtractedField<List<string>> GoalsAddressed { get; set; } = new();
    public ExtractedField<Dictionary<string, string>> GoalProgress { get; set; } = new();
    public ExtractedField<ProgressRatingOverall> ProgressRatingOverall { get; set; } = new();
    public ExtractedField<List<string>> BarriersIdentified { get; set; } = new();
    public ExtractedField<List<string>> StrengthsObserved { get; set; } = new();
    public ExtractedField<TreatmentPhase> TreatmentPhase { get; set; } = new();
}
