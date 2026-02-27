using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SessionSight.Agents.Services;
using SessionSight.Api.Controllers;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<ISessionRepository> _mockRepo;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ILogger<AdminController>> _mockLogger;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _mockRepo = new Mock<ISessionRepository>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(
            _mockRepo.Object,
            _mockScopeFactory.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Reindex_NoSessions_Returns202WithZeroQueued()
    {
        _mockRepo.Setup(r => r.GetSessionsNeedingReindexAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Session>());

        var result = await _controller.Reindex(ct: CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.Value.Should().BeEquivalentTo(new { queued = 0 });
    }

    [Fact]
    public async Task Reindex_WithSessions_Returns202WithCorrectCount()
    {
        var sessions = new List<Session>
        {
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() }
        };

        _mockRepo.Setup(r => r.GetSessionsNeedingReindexAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        SetupScopeFactory();

        var result = await _controller.Reindex(ct: CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.Value.Should().BeEquivalentTo(new { queued = 3 });
    }

    [Fact]
    public async Task Reindex_PatientIdFilter_PassedToRepository()
    {
        var patientId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetSessionsNeedingReindexAsync(patientId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Session>());

        await _controller.Reindex(patientId: patientId, ct: CancellationToken.None);

        _mockRepo.Verify(r => r.GetSessionsNeedingReindexAsync(patientId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reindex_SessionIdFilter_PassedToRepository()
    {
        var sessionId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetSessionsNeedingReindexAsync(null, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Session>());

        await _controller.Reindex(sessionId: sessionId, ct: CancellationToken.None);

        _mockRepo.Verify(r => r.GetSessionsNeedingReindexAsync(null, sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupScopeFactory()
    {
        var mockReindexService = new Mock<IReindexService>();
        mockReindexService.Setup(s => s.ReindexSessionsAsync(It.IsAny<IReadOnlyList<Session>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReindexResult(0, 0, 0));

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IReindexService)))
            .Returns(mockReindexService.Object);

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
    }
}
