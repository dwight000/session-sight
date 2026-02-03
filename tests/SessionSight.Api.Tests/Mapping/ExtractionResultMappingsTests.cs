using FluentAssertions;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Schema;

namespace SessionSight.Api.Tests.Mapping;

public class ExtractionResultMappingsTests
{
    [Fact]
    public void ToDto_MapsAllProperties()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var extractedAt = DateTime.UtcNow;
        var data = new ClinicalExtraction();

        var entity = new ExtractionResult
        {
            Id = id,
            SessionId = sessionId,
            SchemaVersion = "1.0.0",
            ModelUsed = "gpt-4o",
            OverallConfidence = 0.95,
            RequiresReview = true,
            ExtractedAt = extractedAt,
            Data = data
        };

        var dto = entity.ToDto();

        dto.Id.Should().Be(id);
        dto.SessionId.Should().Be(sessionId);
        dto.SchemaVersion.Should().Be("1.0.0");
        dto.ModelUsed.Should().Be("gpt-4o");
        dto.OverallConfidence.Should().Be(0.95);
        dto.RequiresReview.Should().BeTrue();
        dto.ExtractedAt.Should().Be(extractedAt);
        dto.Data.Should().BeSameAs(data);
    }

    [Fact]
    public void ToDto_WithDefaultValues_MapsCorrectly()
    {
        var entity = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid()
        };

        var dto = entity.ToDto();

        dto.SchemaVersion.Should().Be("1.0.0");
        dto.ModelUsed.Should().BeEmpty();
        dto.OverallConfidence.Should().Be(0);
        dto.RequiresReview.Should().BeFalse();
    }

    [Fact]
    public void ToDto_WithLowConfidence_MapsCorrectly()
    {
        var entity = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            OverallConfidence = 0.5,
            RequiresReview = true
        };

        var dto = entity.ToDto();

        dto.OverallConfidence.Should().Be(0.5);
        dto.RequiresReview.Should().BeTrue();
    }
}
