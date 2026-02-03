using FluentAssertions;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Core.Tests.Schema;

public class RiskAssessmentExtractedTests
{
    [Fact]
    public void IsHighRisk_WithNone_ReturnsFalse()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
        };

        risk.IsHighRisk().Should().BeFalse();
    }

    [Fact]
    public void IsHighRisk_WithActiveWithPlanSuicidal_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.ActiveWithPlan },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
        };

        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_WithActiveWithIntentSuicidal_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.ActiveWithIntent },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
        };

        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_WithCurrentSelfHarm_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.Current },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
        };

        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_WithImminentSelfHarm_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.Imminent },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
        };

        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_WithActiveWithPlanHomicidal_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.ActiveWithPlan },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
        };

        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_WithHighRiskLevel_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.High }
        };

        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_WithImminentRiskLevel_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Imminent }
        };

        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_WithModerateRiskLevel_ReturnsFalse()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.Passive },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.Historical },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Moderate }
        };

        risk.IsHighRisk().Should().BeFalse();
    }

    [Fact]
    public void RiskAssessmentExtracted_DefaultValues_AreInitialized()
    {
        var risk = new RiskAssessmentExtracted();

        risk.SuicidalIdeation.Should().NotBeNull();
        risk.SiFrequency.Should().NotBeNull();
        risk.SiIntensity.Should().NotBeNull();
        risk.SelfHarm.Should().NotBeNull();
        risk.ShRecency.Should().NotBeNull();
        risk.HomicidalIdeation.Should().NotBeNull();
        risk.HiTarget.Should().NotBeNull();
        risk.SafetyPlanStatus.Should().NotBeNull();
        risk.ProtectiveFactors.Should().NotBeNull();
        risk.RiskFactors.Should().NotBeNull();
        risk.MeansRestrictionDiscussed.Should().NotBeNull();
        risk.RiskLevelOverall.Should().NotBeNull();
    }
}
