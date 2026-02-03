using FluentAssertions;
using SessionSight.Agents.Models;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Models;

public class RiskAssessmentResultTests
{
    [Fact]
    public void RiskAssessmentResult_DefaultValues_AreInitialized()
    {
        var result = new RiskAssessmentResult();

        result.OriginalExtraction.Should().NotBeNull();
        result.ValidatedExtraction.Should().NotBeNull();
        result.FinalExtraction.Should().NotBeNull();
        result.RequiresReview.Should().BeFalse();
        result.ReviewReasons.Should().NotBeNull();
        result.ReviewReasons.Should().BeEmpty();
        result.Discrepancies.Should().NotBeNull();
        result.Discrepancies.Should().BeEmpty();
        result.KeywordMatches.Should().NotBeNull();
        result.KeywordMatches.Should().BeEmpty();
        result.DeterminedRiskLevel.Should().Be(default);
        result.ModelUsed.Should().BeEmpty();
    }

}
