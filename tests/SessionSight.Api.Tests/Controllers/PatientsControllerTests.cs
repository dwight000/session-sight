using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace SessionSight.Api.Tests.Controllers;

public class PatientsControllerTests
{
    private readonly Mock<IPatientRepository> _mockRepo;
    private readonly Mock<ILogger<PatientsController>> _mockLogger;
    private readonly PatientsController _controller;

    public PatientsControllerTests()
    {
        _mockRepo = new Mock<IPatientRepository>();
        _mockLogger = new Mock<ILogger<PatientsController>>();
        _controller = new PatientsController(_mockRepo.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithPatients()
    {
        var patients = new List<Patient>
        {
            new() { Id = Guid.NewGuid(), ExternalId = "P001", FirstName = "John", LastName = "Doe", DateOfBirth = new DateOnly(1990, 1, 1) }
        };
        _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(patients);

        var result = await _controller.GetAll();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<PatientDto>>().Subject;
        dtos.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_PatientExists_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var patient = new Patient { Id = id, ExternalId = "P001", FirstName = "John", LastName = "Doe" };
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(patient);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_PatientNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Patient?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var request = new CreatePatientRequest("P002", "Jane", "Smith", new DateOnly(1985, 6, 15));
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<Patient>()))
            .ReturnsAsync((Patient p) => { p.Id = Guid.NewGuid(); return p; });

        var result = await _controller.Create(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }
}
