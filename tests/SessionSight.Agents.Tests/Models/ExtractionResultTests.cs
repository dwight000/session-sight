using FluentAssertions;
using SessionSight.Agents.Models;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Models;

public class ExtractionResultTests
{
    [Fact]
    public void ExtractionResult_DefaultValues_AreInitialized()
    {
        var result = new ExtractionResult();

        result.SessionId.Should().BeEmpty();
        result.Data.Should().NotBeNull();
        result.OverallConfidence.Should().Be(0);
        result.RequiresReview.Should().BeFalse();
        result.LowConfidenceFields.Should().NotBeNull();
        result.LowConfidenceFields.Should().BeEmpty();
        result.ModelsUsed.Should().NotBeNull();
        result.ModelsUsed.Should().BeEmpty();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
        result.ToolCallCount.Should().Be(0);
    }

    [Fact]
    public void ExtractionResult_CanSetAllProperties()
    {
        var data = new ClinicalExtraction();

        var result = new ExtractionResult
        {
            SessionId = "session-123",
            Data = data,
            OverallConfidence = 0.95,
            RequiresReview = true,
            LowConfidenceFields = new List<string> { "patientId", "sessionDate" },
            ModelsUsed = new List<string> { "gpt-4o", "gpt-4o-mini" },
            Errors = new List<string> { "Field validation failed" },
            ToolCallCount = 5
        };

        result.SessionId.Should().Be("session-123");
        result.Data.Should().Be(data);
        result.OverallConfidence.Should().Be(0.95);
        result.RequiresReview.Should().BeTrue();
        result.LowConfidenceFields.Should().HaveCount(2);
        result.ModelsUsed.Should().HaveCount(2);
        result.Errors.Should().HaveCount(1);
        result.ToolCallCount.Should().Be(5);
    }

    [Fact]
    public void IsSuccess_WithNoErrors_ReturnsTrue()
    {
        var result = new ExtractionResult
        {
            Errors = new List<string>()
        };

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsSuccess_WithErrors_ReturnsFalse()
    {
        var result = new ExtractionResult
        {
            Errors = new List<string> { "Error 1", "Error 2" }
        };

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsSuccess_DefaultInstance_ReturnsTrue()
    {
        var result = new ExtractionResult();

        result.IsSuccess.Should().BeTrue();
    }
}
