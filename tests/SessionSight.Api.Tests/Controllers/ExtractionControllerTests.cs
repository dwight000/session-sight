using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SessionSight.Agents.Orchestration;
using SessionSight.Api.Controllers;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class ExtractionControllerTests
{
    private readonly Mock<IExtractionOrchestrator> _mockOrchestrator;
    private readonly Mock<ISessionRepository> _mockRepo;
    private readonly Mock<ILogger<ExtractionController>> _mockLogger;
    private readonly ExtractionController _controller;

    public ExtractionControllerTests()
    {
        _mockOrchestrator = new Mock<IExtractionOrchestrator>();
        _mockRepo = new Mock<ISessionRepository>();
        _mockLogger = new Mock<ILogger<ExtractionController>>();
        _controller = new ExtractionController(
            _mockOrchestrator.Object,
            _mockRepo.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task TriggerExtraction_SessionNotFound_ReturnsNotFound()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync((Session?)null);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TriggerExtraction_NoDocument_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Document = null };
        _mockRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TriggerExtraction_AlreadyProcessing_ReturnsConflict()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Status = DocumentStatus.Processing }
        };
        _mockRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
        var conflict = result.Result as ConflictObjectResult;
        conflict!.Value.Should().Be("Extraction already in progress");
    }

    [Fact]
    public async Task TriggerExtraction_AlreadyCompleted_ReturnsConflict()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Status = DocumentStatus.Completed }
        };
        _mockRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task TriggerExtraction_PendingDocument_CallsOrchestratorAndReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Status = DocumentStatus.Pending }
        };
        var orchestrationResult = new OrchestrationResult
        {
            Success = true,
            SessionId = sessionId,
            ExtractionId = Guid.NewGuid(),
            RequiresReview = false
        };

        _mockRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);
        _mockOrchestrator.Setup(o => o.ProcessSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestrationResult);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = result.Result as OkObjectResult;
        ok!.Value.Should().BeEquivalentTo(orchestrationResult);
        _mockOrchestrator.Verify(o => o.ProcessSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerExtraction_FailedDocument_CanRetrigger()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Status = DocumentStatus.Failed }
        };
        var orchestrationResult = new OrchestrationResult
        {
            Success = true,
            SessionId = sessionId,
            ExtractionId = Guid.NewGuid()
        };

        _mockRepo.Setup(r => r.GetByIdAsync(sessionId)).ReturnsAsync(session);
        _mockOrchestrator.Setup(o => o.ProcessSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestrationResult);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
