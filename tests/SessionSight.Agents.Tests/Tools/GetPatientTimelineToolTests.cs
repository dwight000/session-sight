using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Tools;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Tools;

public class GetPatientTimelineToolTests
{
    private readonly ISessionRepository _repository = Substitute.For<ISessionRepository>();
    private readonly GetPatientTimelineTool _tool;

    public GetPatientTimelineToolTests()
    {
        _tool = new GetPatientTimelineTool(_repository);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("get_patient_timeline");
    }

    [Fact]
    public void InputSchema_IsValidJson()
    {
        var schema = _tool.InputSchema.ToString();
        var parsed = JsonDocument.Parse(schema);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("object");
        parsed.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("patientId");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPatient_ReturnsTimeline()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateTestSession(patientId, DateOnly.Parse("2024-01-15"), 1),
            CreateTestSession(patientId, DateOnly.Parse("2024-01-22"), 2),
            CreateTestSession(patientId, DateOnly.Parse("2024-01-29"), 3)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("TotalSessions").GetInt32().Should().Be(3);
        output.RootElement.GetProperty("DateRange").GetString().Should().Be("2024-01-15 to 2024-01-29");
    }

    [Fact]
    public async Task ExecuteAsync_WithDateRange_UsesDateRangeMethod()
    {
        var patientId = Guid.NewGuid();
        _repository.GetByPatientIdInDateRangeAsync(patientId, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>())
            .Returns(new List<Session>());

        var input = BinaryData.FromObjectAsJson(new
        {
            patientId = patientId.ToString(),
            startDate = "2024-01-01",
            endDate = "2024-03-31"
        });
        await _tool.ExecuteAsync(input);

        await _repository.Received(1).GetByPatientIdInDateRangeAsync(
            patientId,
            DateOnly.Parse("2024-01-01"),
            DateOnly.Parse("2024-03-31"));
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSessions_ReturnsEmptyTimeline()
    {
        var patientId = Guid.NewGuid();
        _repository.GetByPatientIdAsync(patientId).Returns(new List<Session>());

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("TotalSessions").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_DetectsRiskChanges()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateTestSessionWithExtraction(patientId, DateOnly.Parse("2024-01-15"), 1, RiskLevelOverall.Low, 5),
            CreateTestSessionWithExtraction(patientId, DateOnly.Parse("2024-01-22"), 2, RiskLevelOverall.High, 5)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        var timeline = output.RootElement.GetProperty("Timeline").EnumerateArray().ToList();
        timeline[1].GetProperty("RiskChange").GetString().Should().Be("Low -> High");
    }

    [Fact]
    public async Task ExecuteAsync_DetectsMoodChanges()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateTestSessionWithExtraction(patientId, DateOnly.Parse("2024-01-15"), 1, RiskLevelOverall.Low, 3),
            CreateTestSessionWithExtraction(patientId, DateOnly.Parse("2024-01-22"), 2, RiskLevelOverall.Low, 7)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        var timeline = output.RootElement.GetProperty("Timeline").EnumerateArray().ToList();
        timeline[1].GetProperty("MoodChange").GetString().Should().Be("improved");
    }

    [Fact]
    public async Task ExecuteAsync_OrdersChronologically()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            CreateTestSession(patientId, DateOnly.Parse("2024-03-01"), 3),
            CreateTestSession(patientId, DateOnly.Parse("2024-01-01"), 1),
            CreateTestSession(patientId, DateOnly.Parse("2024-02-01"), 2)
        };
        _repository.GetByPatientIdAsync(patientId).Returns(sessions);

        var input = BinaryData.FromObjectAsJson(new { patientId = patientId.ToString() });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        var timeline = output.RootElement.GetProperty("Timeline").EnumerateArray().ToList();
        timeline[0].GetProperty("SessionNumber").GetInt32().Should().Be(1);
        timeline[1].GetProperty("SessionNumber").GetInt32().Should().Be(2);
        timeline[2].GetProperty("SessionNumber").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPatientId_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { });

        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("patientId");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidGuid_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { patientId = "not-a-guid" });

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

    private static Session CreateTestSession(Guid patientId, DateOnly date, int sessionNumber)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SessionDate = date,
            SessionNumber = sessionNumber,
            SessionType = SessionType.Individual,
            Modality = SessionModality.InPerson
        };
    }

    private static Session CreateTestSessionWithExtraction(
        Guid patientId, DateOnly date, int sessionNumber,
        RiskLevelOverall riskLevel, int moodScore)
    {
        var session = CreateTestSession(patientId, date, sessionNumber);
        session.Extraction = new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Data = new ClinicalExtraction
            {
                RiskAssessment = new RiskAssessmentExtracted
                {
                    RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = riskLevel }
                },
                MoodAssessment = new MoodAssessmentExtracted
                {
                    SelfReportedMood = new ExtractedField<int> { Value = moodScore }
                }
            }
        };
        return session;
    }
}
