using FluentAssertions;
using SessionSight.Core.ValueObjects;

namespace SessionSight.Core.Tests.ValueObjects;

public class ConfidenceScoreTests
{
    [Theory]
    [InlineData(0.95, 0.9, true)]
    [InlineData(0.9, 0.9, true)]
    [InlineData(0.89, 0.9, false)]
    [InlineData(0.75, 0.7, true)]
    [InlineData(0.65, 0.7, false)]
    [InlineData(0.6, 0.6, true)]
    [InlineData(0.59, 0.6, false)]
    public void MeetsThreshold_ReturnsCorrectResult(double score, double threshold, bool expected)
    {
        var confidence = new ConfidenceScore(score);
        confidence.MeetsThreshold(threshold).Should().Be(expected);
    }

    [Fact]
    public void Constructor_ValueBelowZero_ThrowsArgumentOutOfRange()
    {
        var act = () => new ConfidenceScore(-0.1);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("value");
    }

    [Fact]
    public void Constructor_ValueAboveOne_ThrowsArgumentOutOfRange()
    {
        var act = () => new ConfidenceScore(1.1);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("value");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_ValidValue_StoresCorrectly(double value)
    {
        var confidence = new ConfidenceScore(value);
        confidence.Value.Should().Be(value);
    }
}
