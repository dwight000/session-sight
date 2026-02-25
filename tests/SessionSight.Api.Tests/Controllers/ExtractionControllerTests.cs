using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SessionSight.Api.Controllers;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class ExtractionControllerTests
{
    private readonly Mock<ISessionRepository> _mockRepo;
    private readonly Mock<IDocumentRepository> _mockDocRepo;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ILogger<ExtractionController>> _mockLogger;
    private readonly ExtractionController _controller;

    public ExtractionControllerTests()
    {
        _mockRepo = new Mock<ISessionRepository>();
        _mockDocRepo = new Mock<IDocumentRepository>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<ExtractionController>>();
        _controller = new ExtractionController(
            _mockRepo.Object,
            _mockDocRepo.Object,
            _mockScopeFactory.Object,
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
        result.Should().BeOfType<NotFoundObjectResult>();
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
        result.Should().BeOfType<BadRequestObjectResult>();
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
            sessionId, It.IsAny<DocumentStatus>(), DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        var conflict = result as ConflictObjectResult;
        conflict!.Value.Should().Be("Extraction already in progress or completed");
    }

    [Fact]
    public async Task TriggerExtraction_PendingDocument_Returns202Accepted()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Status = DocumentStatus.Pending }
        };

        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
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

        _mockRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        // Pending → Processing fails (status is Failed, not Pending)
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Pending, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        // Failed → Processing succeeds
        _mockDocRepo.Setup(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.Failed, DocumentStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
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

        // Act
        var result = await _controller.TriggerExtraction(sessionId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        _mockDocRepo.Verify(r => r.TryTransitionDocumentStatusAsync(
            sessionId, DocumentStatus.PartiallyCompleted, DocumentStatus.Processing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
