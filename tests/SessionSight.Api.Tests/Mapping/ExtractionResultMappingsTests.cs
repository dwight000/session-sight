using System.Text.Json;
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
            GuardrailApplied = true,
            HomicidalGuardrailApplied = true,
            HomicidalGuardrailReason = "keyword_present",
            SelfHarmGuardrailApplied = false,
            SelfHarmGuardrailReason = null,
            CriteriaValidationAttempts = 2,
            DiscrepancyCount = 1,
            RiskFieldDecisionsJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    field = "suicidal_ideation",
                    originalValue = "None",
                    reExtractedValue = "Low",
                    finalValue = "Low",
                    ruleApplied = "conservative_merge",
                    criteriaUsed = new[] { "keyword_match" },
                    reasoningUsed = "Elevated based on note content"
                }
            })
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
        dto.RiskDiagnostics.Should().NotBeNull();
        dto.RiskDiagnostics!.GuardrailApplied.Should().BeTrue();
        dto.RiskDiagnostics.HomicidalGuardrail.Should().NotBeNull();
        dto.RiskDiagnostics.HomicidalGuardrail!.Applied.Should().BeTrue();
        dto.RiskDiagnostics.HomicidalGuardrail.Reason.Should().Be("keyword_present");
        dto.RiskDiagnostics.SelfHarmGuardrail.Should().BeNull();
        dto.RiskDiagnostics.CriteriaValidationAttempts.Should().Be(2);
        dto.RiskDiagnostics.DiscrepancyCount.Should().Be(1);
        dto.RiskDiagnostics.FieldDecisions.Should().HaveCount(1);
        var decision = dto.RiskDiagnostics.FieldDecisions[0];
        decision.Field.Should().Be("suicidal_ideation");
        decision.OriginalValue.Should().Be("None");
        decision.ReExtractedValue.Should().Be("Low");
        decision.FinalValue.Should().Be("Low");
        decision.RuleApplied.Should().Be("conservative_merge");
        decision.CriteriaUsed.Should().ContainSingle("keyword_match");
        decision.ReasoningUsed.Should().Be("Elevated based on note content");
    }

    [Fact]
    public void ToDto_WithDefaultValues_ReturnsNullDiagnostics()
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
        dto.RiskDiagnostics.Should().BeNull();
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

    [Fact]
    public void ToDto_WithDiscrepancyCountOnly_ReturnsDiagnostics()
    {
        var entity = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            DiscrepancyCount = 3
        };

        var dto = entity.ToDto();

        dto.RiskDiagnostics.Should().NotBeNull();
        dto.RiskDiagnostics!.DiscrepancyCount.Should().Be(3);
        dto.RiskDiagnostics.GuardrailApplied.Should().BeFalse();
        dto.RiskDiagnostics.FieldDecisions.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithInvalidJson_ReturnsEmptyFieldDecisions()
    {
        var entity = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            GuardrailApplied = true,
            HomicidalGuardrailApplied = true,
            RiskFieldDecisionsJson = "not valid json"
        };

        var dto = entity.ToDto();

        dto.RiskDiagnostics.Should().NotBeNull();
        dto.RiskDiagnostics!.FieldDecisions.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithBothGuardrails_MapsCorrectly()
    {
        var entity = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            GuardrailApplied = true,
            HomicidalGuardrailApplied = true,
            HomicidalGuardrailReason = "explicit threats",
            SelfHarmGuardrailApplied = true,
            SelfHarmGuardrailReason = "self-harm indicators"
        };

        var dto = entity.ToDto();

        dto.RiskDiagnostics.Should().NotBeNull();
        dto.RiskDiagnostics!.GuardrailApplied.Should().BeTrue();
        dto.RiskDiagnostics.HomicidalGuardrail.Should().NotBeNull();
        dto.RiskDiagnostics.HomicidalGuardrail!.Applied.Should().BeTrue();
        dto.RiskDiagnostics.HomicidalGuardrail.Reason.Should().Be("explicit threats");
        dto.RiskDiagnostics.SelfHarmGuardrail.Should().NotBeNull();
        dto.RiskDiagnostics.SelfHarmGuardrail!.Applied.Should().BeTrue();
        dto.RiskDiagnostics.SelfHarmGuardrail.Reason.Should().Be("self-harm indicators");
    }
}
