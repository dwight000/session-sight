using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Tools;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Tools;

public class CompareSessionsToolTests
{
    private readonly ISessionRepository _repository = Substitute.For<ISessionRepository>();
    private readonly CompareSessionsTool _tool;

    public CompareSessionsToolTests()
    {
        _tool = new CompareSessionsTool(_repository);
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("compare_sessions");
    }

    [Fact]
    public async Task ExecuteAsync_WithTwoValidSessions_ReturnsComparison()
    {
        var patientId = Guid.NewGuid();
        var session1 = CreateTestSession(Guid.NewGuid(), patientId, 1, DateOnly.Parse("2025-01-01"), 5, RiskLevelOverall.Low);
        var session2 = CreateTestSession(Guid.NewGuid(), patientId, 2, DateOnly.Parse("2025-01-08"), 7, RiskLevelOverall.Moderate);

        _repository.GetByIdAsync(session1.Id).Returns(session1);
        _repository.GetByIdAsync(session2.Id).Returns(session2);

        var input = BinaryData.FromObjectAsJson(new { sessionIds = new[] { session1.Id.ToString(), session2.Id.ToString() } });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        var output = JsonDocument.Parse(result.Data.ToStream());
        output.RootElement.GetProperty("Sessions").GetArrayLength().Should().Be(2);
        output.RootElement.GetProperty("Changes").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithLessThanTwoIds_ReturnsError()
    {
        var input = BinaryData.FromObjectAsJson(new { sessionIds = new[] { Guid.NewGuid().ToString() } });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least 2");
    }

    [Fact]
    public async Task ExecuteAsync_WithCrossPatientSession_ReturnsError()
    {
        var patient1 = Guid.NewGuid();
        var patient2 = Guid.NewGuid();
        var session1 = CreateTestSession(Guid.NewGuid(), patient1, 1, DateOnly.Parse("2025-01-01"));
        var session2 = CreateTestSession(Guid.NewGuid(), patient2, 2, DateOnly.Parse("2025-01-08"));

        _repository.GetByIdAsync(session1.Id).Returns(session1);
        _repository.GetByIdAsync(session2.Id).Returns(session2);

        _tool.AllowedPatientId = patient1;

        var input = BinaryData.FromObjectAsJson(new { sessionIds = new[] { session1.Id.ToString(), session2.Id.ToString() } });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingSession_ReturnsError()
    {
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();

        _repository.GetByIdAsync(sessionId1).Returns(CreateTestSession(sessionId1, Guid.NewGuid(), 1, DateOnly.Parse("2025-01-01")));
        _repository.GetByIdAsync(sessionId2).Returns((Session?)null);

        var input = BinaryData.FromObjectAsJson(new { sessionIds = new[] { sessionId1.ToString(), sessionId2.ToString() } });
        var result = await _tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    private static Session CreateTestSession(
        Guid sessionId,
        Guid patientId,
        int sessionNumber,
        DateOnly sessionDate,
        int? moodScore = null,
        RiskLevelOverall? riskLevel = null)
    {
        var session = new Session
        {
            Id = sessionId,
            PatientId = patientId,
            SessionDate = sessionDate,
            SessionNumber = sessionNumber,
            SessionType = SessionType.Individual,
            Modality = SessionModality.InPerson,
            DurationMinutes = 50
        };

        if (moodScore.HasValue || riskLevel.HasValue)
        {
            var data = new ClinicalExtraction();
            if (moodScore.HasValue)
            {
                data.MoodAssessment = new MoodAssessmentExtracted
                {
                    SelfReportedMood = new ExtractedField<int> { Value = moodScore.Value }
                };
            }
            if (riskLevel.HasValue)
            {
                data.RiskAssessment = new RiskAssessmentExtracted
                {
                    RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = riskLevel.Value }
                };
            }
            session.Extraction = new ExtractionResult
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                OverallConfidence = 0.9,
                Data = data
            };
        }

        return session;
    }
}
