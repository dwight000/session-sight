using FluentAssertions;
using SessionSight.Agents.Models;

namespace SessionSight.Agents.Tests.Models;

public class RiskDiagnosticsModelTests
{
    [Fact]
    public void RiskFieldDiagnostic_DefaultValues_AreCorrect()
    {
        var diag = new RiskFieldDiagnostic();

        diag.Field.Should().BeEmpty();
        diag.OriginalValue.Should().BeEmpty();
        diag.ReExtractedValue.Should().BeEmpty();
        diag.FinalValue.Should().BeEmpty();
        diag.RuleApplied.Should().BeEmpty();
        diag.OriginalSource.Should().BeNull();
        diag.ReExtractedSource.Should().BeNull();
        diag.FinalSource.Should().BeNull();
        diag.CriteriaUsed.Should().BeEmpty();
        diag.ReasoningUsed.Should().BeEmpty();
    }

    [Fact]
    public void RiskFieldDiagnostic_CanSetAllProperties()
    {
        var diag = new RiskFieldDiagnostic
        {
            Field = "suicidal_ideation",
            OriginalValue = "None",
            ReExtractedValue = "Low",
            FinalValue = "Low",
            RuleApplied = "conservative_merge",
            OriginalSource = "extractor",
            ReExtractedSource = "risk_assessor",
            FinalSource = "risk_assessor",
            CriteriaUsed = ["keyword_match", "context_analysis"],
            ReasoningUsed = "Elevated based on note content"
        };

        diag.Field.Should().Be("suicidal_ideation");
        diag.OriginalValue.Should().Be("None");
        diag.ReExtractedValue.Should().Be("Low");
        diag.FinalValue.Should().Be("Low");
        diag.RuleApplied.Should().Be("conservative_merge");
        diag.OriginalSource.Should().Be("extractor");
        diag.ReExtractedSource.Should().Be("risk_assessor");
        diag.FinalSource.Should().Be("risk_assessor");
        diag.CriteriaUsed.Should().HaveCount(2);
        diag.ReasoningUsed.Should().Be("Elevated based on note content");
    }

    [Fact]
    public void RiskCriteriaEnvelope_CanDeserialize()
    {
        var json = """{"criteria_used": {"self_harm": ["keyword_match"]}}""";
        var envelope = System.Text.Json.JsonSerializer.Deserialize<RiskCriteriaEnvelope>(json);

        envelope.Should().NotBeNull();
        envelope!.CriteriaUsed.Should().ContainKey("self_harm");
    }
}
