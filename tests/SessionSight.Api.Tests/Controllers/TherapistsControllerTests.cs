using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class TherapistsControllerTests
{
    private readonly Mock<ITherapistRepository> _mockRepo;
    private readonly TherapistsController _controller;

    public TherapistsControllerTests()
    {
        _mockRepo = new Mock<ITherapistRepository>();
        _controller = new TherapistsController(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithTherapists()
    {
        var therapists = new List<Therapist>
        {
            new() { Id = Guid.NewGuid(), Name = "Dr. Smith", LicenseNumber = "LIC001", Credentials = "PhD", IsActive = true }
        };
        _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(therapists);

        var result = await _controller.GetAll();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<TherapistDto>>().Subject;
        dtos.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var therapist = new Therapist { Id = id, Name = "Dr. Smith", IsActive = true };
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(therapist);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Therapist?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithTherapist()
    {
        var request = new CreateTherapistRequest("Dr. Jones", "LIC002", "LCSW", true);
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<Therapist>()))
            .ReturnsAsync((Therapist t) => { t.Id = Guid.NewGuid(); return t; });

        var result = await _controller.Create(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Update_WhenFound_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var therapist = new Therapist { Id = id, Name = "Dr. Smith", IsActive = true };
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(therapist);
        _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Therapist>())).Returns(Task.CompletedTask);

        var request = new UpdateTherapistRequest("Dr. Smith Updated", "LIC001-U", "PhD", true);
        var result = await _controller.Update(id, request);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Therapist?)null);

        var request = new UpdateTherapistRequest("Dr. Nobody", null, null, true);
        var result = await _controller.Update(id, request);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_WhenFound_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var therapist = new Therapist { Id = id, Name = "Dr. Smith", IsActive = true };
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(therapist);
        _mockRepo.Setup(r => r.DeleteAsync(id)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Therapist?)null);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NotFoundResult>();
    }
}
