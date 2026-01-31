using FluentAssertions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;

namespace SessionSight.Agents.Tests.Agents;

public class IntakeAgentTests
{
    [Fact]
    public void ExtractJson_PlainJson_ReturnsAsIs()
    {
        var json = """{"isValidTherapyNote": true}""";
        var result = IntakeAgent.ExtractJson(json);
        result.Should().Be(json);
    }

    [Fact]
    public void ExtractJson_MarkdownCodeBlock_ExtractsJson()
    {
        var input = """
            ```json
            {"isValidTherapyNote": true}
            ```
            """;
        var result = IntakeAgent.ExtractJson(input);
        result.Should().Be("""{"isValidTherapyNote": true}""");
    }

    [Fact]
    public void ExtractJson_GenericCodeBlock_ExtractsContent()
    {
        var input = """
            ```
            {"isValidTherapyNote": false}
            ```
            """;
        var result = IntakeAgent.ExtractJson(input);
        result.Should().Be("""{"isValidTherapyNote": false}""");
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsIntakeResult()
    {
        var json = """
            {
                "isValidTherapyNote": true,
                "validationError": null,
                "documentType": "Session Note",
                "sessionDate": "2026-01-15",
                "patientId": "P12345",
                "therapistName": "Dr. Smith",
                "language": "en",
                "estimatedWordCount": 500
            }
            """;
        var document = CreateSampleDocument();

        var result = IntakeAgent.ParseResponse(json, document, "gpt-4o-mini");

        result.IsValidTherapyNote.Should().BeTrue();
        result.ValidationError.Should().BeNull();
        result.Metadata.DocumentType.Should().Be("Session Note");
        result.Metadata.SessionDate.Should().Be(new DateOnly(2026, 1, 15));
        result.Metadata.PatientId.Should().Be("P12345");
        result.Metadata.TherapistName.Should().Be("Dr. Smith");
        result.Metadata.Language.Should().Be("en");
        result.Metadata.EstimatedWordCount.Should().Be(500);
        result.ModelUsed.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void ParseResponse_InvalidDocument_ReturnsValidationError()
    {
        var json = """
            {
                "isValidTherapyNote": false,
                "validationError": "Document appears to be a consent form, not a session note",
                "documentType": "Other",
                "sessionDate": null,
                "patientId": null,
                "therapistName": null,
                "language": "en",
                "estimatedWordCount": 200
            }
            """;
        var document = CreateSampleDocument();

        var result = IntakeAgent.ParseResponse(json, document, "gpt-4o-mini");

        result.IsValidTherapyNote.Should().BeFalse();
        result.ValidationError.Should().Be("Document appears to be a consent form, not a session note");
        result.Metadata.DocumentType.Should().Be("Other");
    }

    [Fact]
    public void ParseResponse_MalformedJson_ReturnsErrorResult()
    {
        var badJson = "not valid json at all";
        var document = CreateSampleDocument();

        var result = IntakeAgent.ParseResponse(badJson, document, "gpt-4o-mini");

        result.IsValidTherapyNote.Should().BeFalse();
        result.ValidationError.Should().Contain("JSON parse error");
    }

    [Fact]
    public void ParseResponse_JsonInCodeBlock_ExtractsAndParses()
    {
        var wrappedJson = """
            ```json
            {
                "isValidTherapyNote": true,
                "validationError": null,
                "documentType": "Progress Report",
                "sessionDate": null,
                "patientId": null,
                "therapistName": null,
                "language": "es",
                "estimatedWordCount": 300
            }
            ```
            """;
        var document = CreateSampleDocument();

        var result = IntakeAgent.ParseResponse(wrappedJson, document, "gpt-4o-mini");

        result.IsValidTherapyNote.Should().BeTrue();
        result.Metadata.DocumentType.Should().Be("Progress Report");
        result.Metadata.Language.Should().Be("es");
    }

    [Fact]
    public void BuildUserPrompt_IncludesDocumentContent()
    {
        var document = new ParsedDocument
        {
            Content = "Session with client discussed anxiety management.",
            Metadata = new ParsedDocumentMetadata
            {
                PageCount = 2,
                FileFormat = "pdf",
                ExtractionConfidence = 0.95
            }
        };

        var prompt = IntakePrompts.BuildUserPrompt(document);

        prompt.Should().Contain("Session with client discussed anxiety management.");
        prompt.Should().Contain("Page count: 2");
        prompt.Should().Contain("File format: pdf");
        prompt.Should().Contain("95%"); // Confidence formatted as percentage
    }

    [Fact]
    public void BuildUserPrompt_TruncatesLongContent()
    {
        var longContent = new string('x', 10000);
        var document = new ParsedDocument
        {
            Content = longContent,
            Metadata = new ParsedDocumentMetadata()
        };

        var prompt = IntakePrompts.BuildUserPrompt(document);

        prompt.Should().Contain("[...truncated...]");
        prompt.Length.Should().BeLessThan(longContent.Length);
    }

    private static ParsedDocument CreateSampleDocument()
    {
        return new ParsedDocument
        {
            Content = "Test session note content",
            MarkdownContent = "# Test\nSession note content",
            Metadata = new ParsedDocumentMetadata
            {
                PageCount = 1,
                FileFormat = "pdf",
                ExtractionConfidence = 0.9
            }
        };
    }
}
