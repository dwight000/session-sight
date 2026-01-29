using FluentAssertions;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Tests.Enums;

public class EnumCompletenessTests
{
    [Theory]
    [InlineData(typeof(SessionType), 8)]
    [InlineData(typeof(SessionModality), 4)]
    [InlineData(typeof(DocumentStatus), 4)]
    [InlineData(typeof(PrimaryConcernCategory), 13)]
    [InlineData(typeof(ConcernSeverity), 4)]
    [InlineData(typeof(ObservedAffect), 10)]
    [InlineData(typeof(AffectCongruence), 3)]
    [InlineData(typeof(MoodChange), 6)]
    [InlineData(typeof(MoodVariability), 3)]
    [InlineData(typeof(EnergyLevel), 4)]
    [InlineData(typeof(SuicidalIdeation), 5)]
    [InlineData(typeof(SiFrequency), 4)]
    [InlineData(typeof(SiIntensity), 4)]
    [InlineData(typeof(SelfHarm), 5)]
    [InlineData(typeof(HomicidalIdeation), 4)]
    [InlineData(typeof(SafetyPlanStatus), 5)]
    [InlineData(typeof(RiskLevelOverall), 4)]
    [InlineData(typeof(SpeechType), 6)]
    [InlineData(typeof(ThoughtProcess), 6)]
    [InlineData(typeof(Cognition), 3)]
    [InlineData(typeof(InsightLevel), 4)]
    [InlineData(typeof(JudgmentLevel), 4)]
    [InlineData(typeof(TechniqueUsed), 20)]
    [InlineData(typeof(TechniqueEffectiveness), 5)]
    [InlineData(typeof(HomeworkCompletion), 4)]
    [InlineData(typeof(MedicationAdherence), 4)]
    [InlineData(typeof(ProgressRatingOverall), 5)]
    [InlineData(typeof(TreatmentPhase), 6)]
    [InlineData(typeof(NextSessionFrequency), 6)]
    [InlineData(typeof(ReferralType), 9)]
    [InlineData(typeof(LevelOfCareRecommendation), 5)]
    public void Enum_HasExpectedValueCount(Type enumType, int expectedCount)
    {
        Enum.GetValues(enumType).Length.Should().Be(expectedCount, $"{enumType.Name} should have {expectedCount} values");
    }
}
