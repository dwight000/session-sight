using FluentAssertions;
using SessionSight.Agents.Models;
using SessionSight.Agents.Services;

namespace SessionSight.Agents.Tests.Models;

public class OptionsTests
{
    [Fact]
    public void RiskAssessorOptions_SectionName_IsCorrect()
    {
        RiskAssessorOptions.SectionName.Should().Be("RiskAssessor");
    }

    [Fact]
    public void RiskAssessorOptions_DefaultValues_AreCorrect()
    {
        var options = new RiskAssessorOptions();

        options.RiskConfidenceThreshold.Should().Be(0.9);
        options.AlwaysReExtract.Should().BeTrue();
        options.EnableKeywordSafetyNet.Should().BeTrue();
        options.UseConservativeMerge.Should().BeTrue();
        options.RequireCriteriaUsed.Should().BeTrue();
        options.CriteriaValidationAttempts.Should().Be(2);
    }

    [Fact]
    public void RiskAssessorOptions_CanSetAllProperties()
    {
        var options = new RiskAssessorOptions
        {
            RiskConfidenceThreshold = 0.85,
            AlwaysReExtract = false,
            EnableKeywordSafetyNet = false,
            UseConservativeMerge = false,
            RequireCriteriaUsed = false,
            CriteriaValidationAttempts = 4
        };

        options.RiskConfidenceThreshold.Should().Be(0.85);
        options.AlwaysReExtract.Should().BeFalse();
        options.EnableKeywordSafetyNet.Should().BeFalse();
        options.UseConservativeMerge.Should().BeFalse();
        options.RequireCriteriaUsed.Should().BeFalse();
        options.CriteriaValidationAttempts.Should().Be(4);
    }

    [Fact]
    public void DocumentIntelligenceOptions_SectionName_IsCorrect()
    {
        DocumentIntelligenceOptions.SectionName.Should().Be("DocumentIntelligence");
    }

    [Fact]
    public void DocumentIntelligenceOptions_DefaultValues_AreCorrect()
    {
        var options = new DocumentIntelligenceOptions();

        options.Endpoint.Should().BeEmpty();
        options.MaxFileSizeBytes.Should().Be(50 * 1024 * 1024); // 50MB
        options.MaxPageCount.Should().Be(30);
    }

    [Fact]
    public void DocumentIntelligenceOptions_CanSetAllProperties()
    {
        var options = new DocumentIntelligenceOptions
        {
            Endpoint = "https://my-docint.cognitiveservices.azure.com/",
            MaxFileSizeBytes = 100 * 1024 * 1024,
            MaxPageCount = 50
        };

        options.Endpoint.Should().Be("https://my-docint.cognitiveservices.azure.com/");
        options.MaxFileSizeBytes.Should().Be(100 * 1024 * 1024);
        options.MaxPageCount.Should().Be(50);
    }
}
