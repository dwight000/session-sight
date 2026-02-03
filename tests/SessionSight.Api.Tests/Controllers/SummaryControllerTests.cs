using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Api.Controllers;
using SessionSight.Core.Entities;
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
}
