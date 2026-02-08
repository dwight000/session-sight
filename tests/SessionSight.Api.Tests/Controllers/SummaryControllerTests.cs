using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;
using CoreExtractionResult = SessionSight.Core.Entities.ExtractionResult;

namespace SessionSight.Api.Tests.Controllers;

public class SummaryControllerTests
{
    private readonly Mock<ISummarizerAgent> _mockSummarizer;
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IPatientRepository> _mockPatientRepo;
    private readonly SummaryController _controller;

    public SummaryControllerTests()
    {
        _mockSummarizer = new Mock<ISummarizerAgent>();
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockPatientRepo = new Mock<IPatientRepository>();
        _controller = new SummaryController(
            _mockSummarizer.Object,
            _mockSessionRepo.Object,
            _mockPatientRepo.Object);
    }

    #region GetSessionSummary Tests

    [Fact]
    public async Task GetSessionSummary_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync((Session?)null);

        var result = await _controller.GetSessionSummary(sessionId);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSessionSummary_NoExtraction_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Extraction = null };
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        var result = await _controller.GetSessionSummary(sessionId);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSessionSummary_HasStoredSummary_ReturnsStoredSummary()
    {
        var sessionId = Guid.NewGuid();
        var storedSummaryJson = """{"oneLiner":"Stored summary","keyPoints":"","interventionsUsed":[],"nextSessionFocus":"","modelUsed":"gpt-4o-mini","generatedAt":"2024-01-01T00:00:00Z"}""";
        var session = new Session
        {
            Id = sessionId,
            Extraction = new CoreExtractionResult
            {
                Id = Guid.NewGuid(),
                Data = new ClinicalExtraction(),
                SummaryJson = storedSummaryJson
            }
        };
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        var result = await _controller.GetSessionSummary(sessionId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value.Should().BeOfType<SessionSummary>().Subject;
        summary.OneLiner.Should().Be("Stored summary");
    }

    [Fact]
    public async Task GetSessionSummary_RegenerateTrue_CallsSummarizer()
    {
        var sessionId = Guid.NewGuid();
        var extractionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Extraction = new CoreExtractionResult
            {
                Id = extractionId,
                Data = new ClinicalExtraction(),
                SummaryJson = """{"oneLiner":"Old summary"}"""
            }
        };
        var newSummary = new SessionSummary { OneLiner = "New summary", ModelUsed = "gpt-4o-mini" };

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);
        _mockSummarizer.Setup(s => s.SummarizeSessionAsync(It.IsAny<SessionSight.Agents.Models.ExtractionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newSummary);

        var result = await _controller.GetSessionSummary(sessionId, regenerate: true);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value.Should().BeOfType<SessionSummary>().Subject;
        summary.OneLiner.Should().Be("New summary");
        _mockSessionRepo.Verify(r => r.UpdateExtractionSummaryAsync(extractionId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetSessionSummary_NoStoredSummary_GeneratesNew()
    {
        var sessionId = Guid.NewGuid();
        var extractionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Extraction = new CoreExtractionResult
            {
                Id = extractionId,
                Data = new ClinicalExtraction(),
                SummaryJson = null
            }
        };
        var newSummary = new SessionSummary { OneLiner = "Generated summary", ModelUsed = "gpt-4o-mini" };

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);
        _mockSummarizer.Setup(s => s.SummarizeSessionAsync(It.IsAny<SessionSight.Agents.Models.ExtractionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newSummary);

        var result = await _controller.GetSessionSummary(sessionId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value.Should().BeOfType<SessionSummary>().Subject;
        summary.OneLiner.Should().Be("Generated summary");
    }

    #endregion

    #region GetPatientSummary Tests

    [Fact]
    public async Task GetPatientSummary_PatientNotFound_ReturnsNotFound()
    {
        var patientId = Guid.NewGuid();
        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync((Patient?)null);

        var result = await _controller.GetPatientSummary(patientId, null, null);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPatientSummary_PatientExists_ReturnsSummary()
    {
        var patientId = Guid.NewGuid();
        var patient = new Patient { Id = patientId, ExternalId = "P001" };
        var summary = new PatientSummary
        {
            PatientId = patientId,
            ProgressNarrative = "Good progress",
            MoodTrend = MoodTrend.Improving
        };

        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync(patient);
        _mockSummarizer.Setup(s => s.SummarizePatientAsync(patientId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var result = await _controller.GetPatientSummary(patientId, null, null);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var patientSummary = okResult.Value.Should().BeOfType<PatientSummary>().Subject;
        patientSummary.ProgressNarrative.Should().Be("Good progress");
    }

    [Fact]
    public async Task GetPatientSummary_WithDateRange_PassesDatesToSummarizer()
    {
        var patientId = Guid.NewGuid();
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 12, 31);
        var patient = new Patient { Id = patientId };
        var summary = new PatientSummary { PatientId = patientId };

        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync(patient);
        _mockSummarizer.Setup(s => s.SummarizePatientAsync(patientId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        await _controller.GetPatientSummary(patientId, startDate, endDate);

        _mockSummarizer.Verify(s => s.SummarizePatientAsync(patientId, startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetPatientRiskTrend Tests

    [Fact]
    public async Task GetPatientRiskTrend_PatientNotFound_ReturnsNotFound()
    {
        var patientId = Guid.NewGuid();
        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync((Patient?)null);

        var result = await _controller.GetPatientRiskTrend(patientId, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPatientRiskTrend_InvalidDateRange_ReturnsBadRequest()
    {
        var patientId = Guid.NewGuid();

        var result = await _controller.GetPatientRiskTrend(patientId, new DateOnly(2024, 2, 1), new DateOnly(2024, 1, 1));

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPatientRiskTrend_ValidData_ReturnsOrderedPointsAndEscalation()
    {
        var patientId = Guid.NewGuid();
        var patient = new Patient { Id = patientId, ExternalId = "P001" };
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 1, 31);

        var session1 = CreateSessionWithRisk(patientId, new DateOnly(2024, 1, 5), 1, RiskLevelOverall.Low, 4, false);
        var session2 = CreateSessionWithRisk(patientId, new DateOnly(2024, 1, 12), 2, RiskLevelOverall.High, 3, true);
        var session3 = new Session
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SessionDate = new DateOnly(2024, 1, 20),
            SessionNumber = 3
        };

        _mockPatientRepo.Setup(r => r.GetByIdAsync(patientId)).ReturnsAsync(patient);
        _mockSessionRepo
            .Setup(r => r.GetByPatientIdInDateRangeAsync(patientId, startDate, endDate))
            .ReturnsAsync(new[] { session2, session3, session1 });

        var result = await _controller.GetPatientRiskTrend(patientId, startDate, endDate);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var trend = okResult.Value.Should().BeOfType<PatientRiskTrendDto>().Subject;

        trend.PatientId.Should().Be(patientId);
        trend.TotalSessions.Should().Be(3);
        trend.Period.Start.Should().Be(startDate);
        trend.Period.End.Should().Be(endDate);

        trend.Points.Should().HaveCount(3);
        trend.Points[0].SessionId.Should().Be(session1.Id);
        trend.Points[0].RiskLevel.Should().Be("Low");
        trend.Points[0].RiskScore.Should().Be(0);
        trend.Points[1].SessionId.Should().Be(session2.Id);
        trend.Points[1].RiskLevel.Should().Be("High");
        trend.Points[1].RiskScore.Should().Be(2);
        trend.Points[1].RequiresReview.Should().BeTrue();
        trend.Points[2].SessionId.Should().Be(session3.Id);
        trend.Points[2].RiskLevel.Should().BeNull();
        trend.Points[2].RiskScore.Should().BeNull();

        trend.LatestRiskLevel.Should().Be("High");
        trend.HasEscalation.Should().BeTrue();
    }

    #endregion

    #region GetPracticeSummary Tests

    [Fact]
    public async Task GetPracticeSummary_InvalidDateRange_ReturnsBadRequest()
    {
        var startDate = new DateOnly(2024, 12, 31);
        var endDate = new DateOnly(2024, 1, 1); // End before start

        var result = await _controller.GetPracticeSummary(startDate, endDate);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPracticeSummary_ValidDateRange_ReturnsSummary()
    {
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 12, 31);
        var summary = new PracticeSummary
        {
            TotalSessions = 100,
            TotalPatients = 25,
            Period = new DateRange { Start = startDate, End = endDate }
        };

        _mockSummarizer.Setup(s => s.SummarizePracticeAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var result = await _controller.GetPracticeSummary(startDate, endDate);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var practiceSummary = okResult.Value.Should().BeOfType<PracticeSummary>().Subject;
        practiceSummary.TotalSessions.Should().Be(100);
        practiceSummary.TotalPatients.Should().Be(25);
    }

    [Fact]
    public async Task GetPracticeSummary_SameDayRange_IsValid()
    {
        var date = new DateOnly(2024, 6, 15);
        var summary = new PracticeSummary { TotalSessions = 5 };

        _mockSummarizer.Setup(s => s.SummarizePracticeAsync(date, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var result = await _controller.GetPracticeSummary(date, date);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    private static Session CreateSessionWithRisk(
        Guid patientId,
        DateOnly sessionDate,
        int sessionNumber,
        RiskLevelOverall riskLevel,
        int moodScore,
        bool requiresReview)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SessionDate = sessionDate,
            SessionNumber = sessionNumber,
            Extraction = new CoreExtractionResult
            {
                Id = Guid.NewGuid(),
                RequiresReview = requiresReview,
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
            }
        };
    }
}
