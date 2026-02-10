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
            Data = data,
            CriteriaValidationAttemptsUsed = 2,
            HomicidalGuardrailApplied = true,
            HomicidalGuardrailReason = "keyword_present",
            SelfHarmGuardrailApplied = false,
            SelfHarmGuardrailReason = null,
            RiskDecisionsJson = """[{"field":"suicidal_ideation"}]"""
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
        dto.CriteriaValidationAttemptsUsed.Should().Be(2);
        dto.HomicidalGuardrailApplied.Should().BeTrue();
        dto.HomicidalGuardrailReason.Should().Be("keyword_present");
        dto.SelfHarmGuardrailApplied.Should().BeFalse();
        dto.SelfHarmGuardrailReason.Should().BeNull();
        dto.RiskDecisionsJson.Should().NotBeNull();
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
        dto.CriteriaValidationAttemptsUsed.Should().Be(1);
        dto.HomicidalGuardrailApplied.Should().BeFalse();
        dto.SelfHarmGuardrailApplied.Should().BeFalse();
        dto.RiskDecisionsJson.Should().BeNull();
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
