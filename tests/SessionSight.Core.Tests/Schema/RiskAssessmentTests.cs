using FluentAssertions;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Core.Tests.Schema;

public class RiskAssessmentTests
{
    [Fact]
    public void IsHighRisk_ActiveWithPlan_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.ActiveWithPlan, Confidence = 0.95 }
        };
        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_ActiveWithIntent_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.ActiveWithIntent, Confidence = 0.95 }
        };
        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_SelfHarmCurrent_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.Current, Confidence = 0.9 }
        };
        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_SelfHarmImminent_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.Imminent, Confidence = 0.9 }
        };
        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_HomicidalActiveWithPlan_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.ActiveWithPlan, Confidence = 0.9 }
        };
        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_RiskLevelHigh_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.High, Confidence = 0.9 }
        };
        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_RiskLevelImminent_ReturnsTrue()
    {
        var risk = new RiskAssessmentExtracted
        {
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Imminent, Confidence = 0.9 }
        };
        risk.IsHighRisk().Should().BeTrue();
    }

    [Fact]
    public void IsHighRisk_NoRiskIndicators_ReturnsFalse()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None, Confidence = 0.95 },
            SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None, Confidence = 0.95 },
            HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None, Confidence = 0.95 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low, Confidence = 0.95 }
        };
        risk.IsHighRisk().Should().BeFalse();
    }

    [Fact]
    public void IsHighRisk_PassiveSI_ReturnsFalse()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.Passive, Confidence = 0.9 },
            RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Moderate, Confidence = 0.9 }
        };
        risk.IsHighRisk().Should().BeFalse();
    }

    [Fact]
    public void IsHighRisk_DefaultValues_ReturnsFalse()
    {
        var risk = new RiskAssessmentExtracted();
        risk.IsHighRisk().Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_AreInitialized()
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
