using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class NextStepsExtracted
{
    public ExtractedField<DateOnly> NextSessionDate { get; set; } = new();
    public ExtractedField<NextSessionFrequency> NextSessionFrequency { get; set; } = new();
    public ExtractedField<string> NextSessionFocus { get; set; } = new();
    public ExtractedField<List<string>> ReferralsMade { get; set; } = new();
    public ExtractedField<List<ReferralType>> ReferralTypes { get; set; } = new();
    public ExtractedField<List<string>> CoordinationNeeded { get; set; } = new();
    public ExtractedField<LevelOfCareRecommendation> LevelOfCareRecommendation { get; set; } = new();
    public ExtractedField<string> DischargePlanning { get; set; } = new();
}
