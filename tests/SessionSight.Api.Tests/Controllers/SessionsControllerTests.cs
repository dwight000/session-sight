using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class SessionsControllerTests
{
    private readonly Mock<ISessionRepository> _mockRepo;
    private readonly SessionsController _controller;

    public SessionsControllerTests()
    {
        _mockRepo = new Mock<ISessionRepository>();
        _controller = new SessionsController(_mockRepo.Object);
    }

    [Fact]
    public async Task GetById_SessionExists_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var session = new Session
        {
            Id = id,
            PatientId = Guid.NewGuid(),
            TherapistId = Guid.NewGuid(),
            SessionDate = new DateOnly(2026, 1, 15),
            SessionType = SessionType.Individual,
            Modality = SessionModality.InPerson,
            SessionNumber = 1
        };
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(session);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_SessionNotFound_ReturnsNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Session?)null);

        var result = await _controller.GetById(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var request = new CreateSessionRequest(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 20),
            SessionType.Individual, SessionModality.InPerson, 50, 1);
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<Session>()))
            .ReturnsAsync((Session s) => { s.Id = Guid.NewGuid(); return s; });

        var result = await _controller.Create(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }
}
