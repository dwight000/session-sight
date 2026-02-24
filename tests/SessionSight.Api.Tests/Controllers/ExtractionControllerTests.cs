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
    private readonly Mock<IDocumentRepository> _mockDocRepo;
    private readonly Mock<ILogger<ExtractionController>> _mockLogger;
    private readonly ExtractionController _controller;

    public ExtractionControllerTests()
    {
        _mockOrchestrator = new Mock<IExtractionOrchestrator>();
        _mockRepo = new Mock<ISessionRepository>();
        _mockDocRepo = new Mock<IDocumentRepository>();
        _mockLogger = new Mock<ILogger<ExtractionController>>();
        _controller = new ExtractionController(
            _mockOrchestrator.Object,
            _mockRepo.Object,
            _mockDocRepo.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task TriggerExtraction_SessionNotFound_ReturnsNotFound()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync((Session?)null);

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
        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TriggerExtraction_TransitionFails_ReturnsConflict()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Status = DocumentStatus.Processing }
        };
        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
        var conflict = result.Result as ConflictObjectResult;
        conflict!.Value.Should().Be("Extraction already in progress or completed");
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

        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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
        // Arrange — Failed documents can transition to Processing via fallback
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

        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        // Pending → Processing fails (status is Failed, not Pending)
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        // Failed → Processing succeeds
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Failed, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockOrchestrator.Setup(o => o.ProcessSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestrationResult);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TriggerExtraction_PartiallyCompletedDocument_CanRetrigger()
    {
        // Arrange — PartiallyCompleted documents can transition to Processing via third fallback
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Status = DocumentStatus.PartiallyCompleted }
        };
        var orchestrationResult = new OrchestrationResult
        {
            Success = true,
            SessionId = sessionId,
            ExtractionId = Guid.NewGuid()
        };

        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        // Pending → Processing fails
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        // Failed → Processing fails
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Failed, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        // PartiallyCompleted → Processing succeeds
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.PartiallyCompleted, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockOrchestrator.Setup(o => o.ProcessSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestrationResult);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mockDocRepo.Verify(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.PartiallyCompleted, DocumentStatus.Processing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
