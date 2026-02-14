using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Tests.Controllers;

public class ProcessingJobsControllerTests
{
    private readonly Mock<IProcessingJobRepository> _mockRepo;
    private readonly ProcessingJobsController _controller;

    public ProcessingJobsControllerTests()
    {
        _mockRepo = new Mock<IProcessingJobRepository>();
        _controller = new ProcessingJobsController(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithJobs()
    {
        var jobs = new List<ProcessingJob>
        {
            new() { Id = Guid.NewGuid(), JobKey = "job-001", Status = JobStatus.Completed, CreatedAt = DateTime.UtcNow }
        };
        _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(jobs);

        var result = await _controller.GetAll();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ProcessingJobDto>>().Subject;
        dtos.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var job = new ProcessingJob { Id = id, JobKey = "job-001", Status = JobStatus.Processing };
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(job);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((ProcessingJob?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
