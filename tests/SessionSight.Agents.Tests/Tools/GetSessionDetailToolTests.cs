using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Tools;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Tools;

public class GetSessionDetailToolTests
{
    private readonly ISessionRepository _repository = Substitute.For<ISessionRepository>();
    private readonly GetSessionDetailTool _tool;

    public GetSessionDetailToolTests()
    {
        _tool = new GetSessionDetailTool(_repository);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("get_session_detail");
    }

    [Fact]
    public void InputSchema_IsValidJson()
    {
        var schema = _tool.InputSchema.ToString();
        var parsed = JsonDocument.Parse(schema);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("object");
        parsed.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("sessionId");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidSession_ReturnsDetail()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        session.Extraction = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OverallConfidence = 0.9,
            Data = new ClinicalExtraction
            {
                MoodAssessment = new MoodAssessmentExtracted
                {
                    SelfReportedMood = new ExtractedField<int> { Value = 7 },
                    MoodChangeFromLast = new ExtractedField<MoodChange> { Value = MoodChange.Improved }
                },
                RiskAssessment = new RiskAssessmentExtracted
                {
                    RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = RiskLevelOverall.Low }
                },
                Diagnoses = new DiagnosesExtracted
                {
                    PrimaryDiagnosis = new ExtractedField<string> { Value = "Generalized Anxiety" }
                },
                Interventions = new InterventionsExtracted
                {
                    TechniquesUsed = new ExtractedField<List<TechniqueUsed>> { Value = [TechniqueUsed.Cbt] }
                },
                PresentingConcerns = new PresentingConcernsExtracted
                {
                    PrimaryConcern = new ExtractedField<string> { Value = "Work stress" }
                },
                NextSteps = new NextStepsExtracted
                {
                    NextSessionFocus = new ExtractedField<string> { Value = "Coping strategies" }
                }
            }
        };
        _repository.GetByIdAsync(sessionId).Returns(session);

        var input = BinaryData.FromObjectAsJson(new { sessionId = sessionId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("HasExtraction").GetBoolean().Should().BeTrue();
        output.RootElement.GetProperty("MoodScore").GetInt32().Should().Be(7);
        output.RootElement.GetProperty("RiskLevel").GetString().Should().Be("Low");
        output.RootElement.GetProperty("PrimaryDiagnosis").GetString().Should().Be("Generalized Anxiety");
    }

    [Fact]
    public async Task ExecuteAsync_SessionNotFound_ReturnsError()
    {
        var sessionId = Guid.NewGuid();
        _repository.GetByIdAsync(sessionId).Returns((Session?)null);

        var input = BinaryData.FromObjectAsJson(new { sessionId = sessionId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_NoExtraction_ReturnsPartialWithFlag()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        session.Extraction = null;
        _repository.GetByIdAsync(sessionId).Returns(session);

        var input = BinaryData.FromObjectAsJson(new { sessionId = sessionId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("HasExtraction").GetBoolean().Should().BeFalse();
        output.RootElement.GetProperty("SessionNumber").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingSessionId_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("sessionId");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidGuid_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { sessionId = "not-a-guid" });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("GUID");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidJson_ReturnsError()
    {
        var input = BinaryData.FromString("not valid json");

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid JSON");
    }

    private static Session CreateTestSession(Guid sessionId)
    {
        return new Session
        {
            Id = sessionId,
            PatientId = Guid.NewGuid(),
            SessionDate = DateOnly.FromDateTime(DateTime.Today),
            SessionNumber = 3,
            SessionType = SessionType.Individual,
            Modality = SessionModality.InPerson,
            DurationMinutes = 50
        };
    }
}
