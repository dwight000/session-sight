using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SessionSight.Agents.Services;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Api.Tests.Controllers;

public class DocumentsControllerStepsTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IExtractionStepRepository> _stepRepositoryMock;
    private readonly DocumentsController _controller;

    public DocumentsControllerStepsTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _stepRepositoryMock = new Mock<IExtractionStepRepository>();
        _controller = new DocumentsController(
            _sessionRepositoryMock.Object,
            _stepRepositoryMock.Object,
            new Mock<IDocumentStorage>().Object,
            new Mock<ISearchIndexService>().Object,
            new Mock<ILogger<DocumentsController>>().Object,
            Options.Create(new DocumentIntelligenceOptions()));
    }

    [Fact]
    public async Task GetExtractionSteps_SessionNotFound_Returns404()
    {
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(null as Session);

        var result = await _controller.GetExtractionSteps(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetExtractionSteps_NoExtraction_Returns404()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(new Session { Id = sessionId, Extraction = null });

        var result = await _controller.GetExtractionSteps(sessionId);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetExtractionSteps_WithSteps_Returns200WithCorrectShape()
    {
        var sessionId = Guid.NewGuid();
        var extractionId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(new Session
            {
                Id = sessionId,
                Extraction = new ExtractionResult { Id = extractionId, SessionId = sessionId }
            });

        _stepRepositoryMock.Setup(r => r.GetStepsByExtractionIdAsync(extractionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExtractionStep>
            {
                new()
                {
                    Id = stepId,
                    ExtractionId = extractionId,
                    StepName = ExtractionStepName.Intake,
                    Status = ExtractionStepStatus.Succeeded,
                    StepOrder = 2,
                    StartedAt = DateTime.UtcNow,
                    DurationMs = 500,
                    ModelUsed = "gpt-4.1-nano",
                    InputTokens = 100,
                    OutputTokens = 50,
                    TotalTokens = 150,
                    ToolCalls = new List<ExtractionToolCall>()
                }
            });

        var result = await _controller.GetExtractionSteps(sessionId);

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value.Should().BeOfType<ExtractionStepsResponseDto>().Subject;
        response.ExtractionId.Should().Be(extractionId);
        response.Steps.Should().HaveCount(1);
        response.Steps[0].StepName.Should().Be("Intake");
        response.Steps[0].Status.Should().Be("Succeeded");
        response.Steps[0].TotalTokens.Should().Be(150);
    }

    [Fact]
    public async Task GetExtractionSteps_ToolCallsIncludedInResponse()
    {
        var sessionId = Guid.NewGuid();
        var extractionId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(new Session
            {
                Id = sessionId,
                Extraction = new ExtractionResult { Id = extractionId, SessionId = sessionId }
            });

        _stepRepositoryMock.Setup(r => r.GetStepsByExtractionIdAsync(extractionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExtractionStep>
            {
                new()
                {
                    Id = stepId,
                    ExtractionId = extractionId,
                    StepName = ExtractionStepName.ClinicalExtract,
                    Status = ExtractionStepStatus.Succeeded,
                    StepOrder = 3,
                    StartedAt = DateTime.UtcNow,
                    DurationMs = 2000,
                    ModelUsed = "gpt-4.1-mini",
                    ToolCalls = new List<ExtractionToolCall>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            StepId = stepId,
                            ToolName = "ValidateSchema",
                            LoopRound = 0,
                            Succeeded = true,
                            DurationMs = 50,
                            CalledAt = DateTime.UtcNow
                        }
                    }
                }
            });

        var result = await _controller.GetExtractionSteps(sessionId);

        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value.Should().BeOfType<ExtractionStepsResponseDto>().Subject;
        response.Steps[0].ToolCalls.Should().HaveCount(1);
        response.Steps[0].ToolCalls[0].ToolName.Should().Be("ValidateSchema");
        response.Steps[0].ToolCalls[0].LoopRound.Should().Be(0);
    }
}
