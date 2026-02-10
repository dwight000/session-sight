using FluentAssertions;
using SessionSight.Api.DTOs;
using SessionSight.Core.Schema;

namespace SessionSight.Api.Tests.DTOs;

public class DtoTests
{
    [Fact]
    public void ExtractionResultDto_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var extractedAt = DateTime.UtcNow;
        var data = new ClinicalExtraction();

        var dto = new ExtractionResultDto(
            id,
            sessionId,
            "1.0.0",
            "gpt-4o",
            0.95,
            true,
            extractedAt,
            data,
            null);

        dto.Id.Should().Be(id);
        dto.SessionId.Should().Be(sessionId);
        dto.SchemaVersion.Should().Be("1.0.0");
        dto.ModelUsed.Should().Be("gpt-4o");
        dto.OverallConfidence.Should().Be(0.95);
        dto.RequiresReview.Should().BeTrue();
        dto.ExtractedAt.Should().Be(extractedAt);
        dto.Data.Should().BeSameAs(data);
        dto.RiskDiagnostics.Should().BeNull();
    }

    [Fact]
    public void ExtractionResultDto_WithRiskDiagnostics_MapsCorrectly()
    {
        var diagnostics = new RiskDiagnosticsDto(
            GuardrailApplied: true,
            HomicidalGuardrail: new GuardrailDetailDto(true, "keyword_present"),
            SelfHarmGuardrail: null,
            CriteriaValidationAttempts: 2,
            DiscrepancyCount: 1,
            FieldDecisions: [new RiskFieldDecisionDto(
                "suicidal_ideation", "None", "Low", "Low",
                "conservative_merge", ["keyword_match"], "Elevated based on note content")]);

        var dto = new ExtractionResultDto(
            Guid.NewGuid(), Guid.NewGuid(), "1.0.0", "gpt-4o",
            0.95, true, DateTime.UtcNow, new ClinicalExtraction(),
            diagnostics);

        dto.RiskDiagnostics.Should().NotBeNull();
        dto.RiskDiagnostics!.GuardrailApplied.Should().BeTrue();
        dto.RiskDiagnostics.HomicidalGuardrail.Should().NotBeNull();
        dto.RiskDiagnostics.HomicidalGuardrail!.Applied.Should().BeTrue();
        dto.RiskDiagnostics.HomicidalGuardrail.Reason.Should().Be("keyword_present");
        dto.RiskDiagnostics.SelfHarmGuardrail.Should().BeNull();
        dto.RiskDiagnostics.CriteriaValidationAttempts.Should().Be(2);
        dto.RiskDiagnostics.DiscrepancyCount.Should().Be(1);
        dto.RiskDiagnostics.FieldDecisions.Should().HaveCount(1);
        dto.RiskDiagnostics.FieldDecisions[0].Field.Should().Be("suicidal_ideation");
    }

    [Fact]
    public void ExtractionResultDto_RecordEquality_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var extractedAt = DateTime.UtcNow;
        var data = new ClinicalExtraction();

        var dto1 = new ExtractionResultDto(id, sessionId, "1.0.0", "gpt-4o", 0.95, true, extractedAt, data, null);
        var dto2 = new ExtractionResultDto(id, sessionId, "1.0.0", "gpt-4o", 0.95, true, extractedAt, data, null);

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void GuardrailDetailDto_RecordEquality_WorksCorrectly()
    {
        var dto1 = new GuardrailDetailDto(true, "keyword_present");
        var dto2 = new GuardrailDetailDto(true, "keyword_present");

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void UploadDocumentResponse_CanBeCreated()
    {
        var documentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var response = new UploadDocumentResponse(
            documentId,
            sessionId,
            "therapy-note.pdf",
            "https://storage.blob.core.windows.net/documents/xyz.pdf",
            "Pending");

        response.DocumentId.Should().Be(documentId);
        response.SessionId.Should().Be(sessionId);
        response.OriginalFileName.Should().Be("therapy-note.pdf");
        response.BlobUri.Should().Be("https://storage.blob.core.windows.net/documents/xyz.pdf");
        response.Status.Should().Be("Pending");
    }

    [Fact]
    public void UploadDocumentResponse_RecordEquality_WorksCorrectly()
    {
        var documentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var response1 = new UploadDocumentResponse(documentId, sessionId, "file.pdf", "uri", "Pending");
        var response2 = new UploadDocumentResponse(documentId, sessionId, "file.pdf", "uri", "Pending");

        response1.Should().Be(response2);
    }

    [Fact]
    public void UploadDocumentResponse_WithDifferentStatus_NotEqual()
    {
        var documentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var response1 = new UploadDocumentResponse(documentId, sessionId, "file.pdf", "uri", "Pending");
        var response2 = new UploadDocumentResponse(documentId, sessionId, "file.pdf", "uri", "Completed");

        response1.Should().NotBe(response2);
    }
}
