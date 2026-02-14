using Microsoft.AspNetCore.Mvc;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api/processing-jobs")]
public class ProcessingJobsController : ControllerBase
{
    private readonly IProcessingJobRepository _repository;

    public ProcessingJobsController(IProcessingJobRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessingJobDto>>> GetAll()
    {
        var jobs = await _repository.GetAllAsync();
        return Ok(jobs.Select(j => j.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProcessingJobDto>> GetById(Guid id)
    {
        var job = await _repository.GetByIdAsync(id);
        if (job is null) return NotFound();
        return Ok(job.ToDto());
    }
}
