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

    [Fact]
    public void RiskAssessmentResult_CanSetAllProperties()
    {
        var original = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None }
        };
        var validated = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.Passive }
        };
        var final = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.Passive }
        };

        var result = new RiskAssessmentResult
        {
            OriginalExtraction = original,
            ValidatedExtraction = validated,
            FinalExtraction = final,
            RequiresReview = true,
            ReviewReasons = new List<string> { "Discrepancy found", "High risk" },
            Discrepancies = new List<RiskDiscrepancy>
            {
                new RiskDiscrepancy { FieldName = "SuicidalIdeation" }
            },
            KeywordMatches = new List<string> { "suicide", "self-harm" },
            DeterminedRiskLevel = RiskLevelOverall.Moderate,
            ModelUsed = "gpt-4o"
        };

        result.OriginalExtraction.Should().Be(original);
        result.ValidatedExtraction.Should().Be(validated);
        result.FinalExtraction.Should().Be(final);
        result.RequiresReview.Should().BeTrue();
        result.ReviewReasons.Should().HaveCount(2);
        result.Discrepancies.Should().HaveCount(1);
        result.KeywordMatches.Should().HaveCount(2);
        result.DeterminedRiskLevel.Should().Be(RiskLevelOverall.Moderate);
        result.ModelUsed.Should().Be("gpt-4o");
    }
}
