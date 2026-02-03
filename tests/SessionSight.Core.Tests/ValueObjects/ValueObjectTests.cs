using FluentAssertions;
using SessionSight.Core.ValueObjects;

namespace SessionSight.Core.Tests.ValueObjects;

public class ValueObjectTests
{
    [Fact]
    public void ConfidenceScore_ValidValue_CreatesSuccessfully()
    {
        var score = new ConfidenceScore(0.85);
        score.Value.Should().Be(0.85);
    }

    [Fact]
    public void ConfidenceScore_Zero_CreatesSuccessfully()
    {
        var score = new ConfidenceScore(0.0);
        score.Value.Should().Be(0.0);
    }

    [Fact]
    public void ConfidenceScore_One_CreatesSuccessfully()
    {
        var score = new ConfidenceScore(1.0);
        score.Value.Should().Be(1.0);
    }

    [Fact]
    public void ConfidenceScore_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new ConfidenceScore(-0.1);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("value");
    }

    [Fact]
    public void ConfidenceScore_ValueGreaterThanOne_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new ConfidenceScore(1.1);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("value");
    }

    [Fact]
    public void ConfidenceScore_MeetsThreshold_ReturnsTrueWhenAbove()
    {
        var score = new ConfidenceScore(0.9);
        score.MeetsThreshold(0.8).Should().BeTrue();
    }

    [Fact]
    public void ConfidenceScore_MeetsThreshold_ReturnsTrueWhenEqual()
    {
        var score = new ConfidenceScore(0.8);
        score.MeetsThreshold(0.8).Should().BeTrue();
    }

    [Fact]
    public void ConfidenceScore_MeetsThreshold_ReturnsFalseWhenBelow()
    {
        var score = new ConfidenceScore(0.7);
        score.MeetsThreshold(0.8).Should().BeFalse();
    }

    [Fact]
    public void SourceMapping_DefaultValues_AreInitialized()
    {
        var mapping = new SourceMapping();

        mapping.Text.Should().BeEmpty();
        mapping.StartChar.Should().Be(0);
        mapping.EndChar.Should().Be(0);
        mapping.Section.Should().BeNull();
    }

    [Fact]
    public void SourceMapping_CanSetAllProperties()
    {
        var mapping = new SourceMapping
        {
            Text = "Patient reports anxiety",
            StartChar = 100,
            EndChar = 125,
            Section = "Chief Complaint"
        };

        mapping.Text.Should().Be("Patient reports anxiety");
        mapping.StartChar.Should().Be(100);
        mapping.EndChar.Should().Be(125);
        mapping.Section.Should().Be("Chief Complaint");
    }
}
