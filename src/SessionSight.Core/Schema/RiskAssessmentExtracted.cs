using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class RiskAssessmentExtracted
{
    public ExtractedField<SuicidalIdeation> SuicidalIdeation { get; set; } = new();
    public ExtractedField<SiFrequency> SiFrequency { get; set; } = new();
    public ExtractedField<SiIntensity> SiIntensity { get; set; } = new();
    public ExtractedField<SelfHarm> SelfHarm { get; set; } = new();
    public ExtractedField<string> ShRecency { get; set; } = new();
    public ExtractedField<HomicidalIdeation> HomicidalIdeation { get; set; } = new();
    public ExtractedField<string> HiTarget { get; set; } = new();
    public ExtractedField<SafetyPlanStatus> SafetyPlanStatus { get; set; } = new();
    public ExtractedField<List<string>> ProtectiveFactors { get; set; } = new();
    public ExtractedField<List<string>> RiskFactors { get; set; } = new();
    public ExtractedField<bool> MeansRestrictionDiscussed { get; set; } = new();
    public ExtractedField<RiskLevelOverall> RiskLevelOverall { get; set; } = new();

    public bool IsHighRisk()
    {
        return SuicidalIdeation.Value is Enums.SuicidalIdeation.ActiveWithPlan
                   or Enums.SuicidalIdeation.ActiveWithIntent
               || SelfHarm.Value is Enums.SelfHarm.Current
                   or Enums.SelfHarm.Imminent
               || HomicidalIdeation.Value is Enums.HomicidalIdeation.ActiveWithPlan
               || RiskLevelOverall.Value is Enums.RiskLevelOverall.High
                   or Enums.RiskLevelOverall.Imminent;
    }
}
