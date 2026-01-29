using Microsoft.AspNetCore.Mvc;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientRepository _repository;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(IPatientRepository repository, ILogger<PatientsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PatientDto>>> GetAll()
    {
        var patients = await _repository.GetAllAsync();
        return Ok(patients.Select(p => p.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientDto>> GetById(Guid id)
    {
        var patient = await _repository.GetByIdAsync(id);
        if (patient is null) return NotFound();
        return Ok(patient.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<PatientDto>> Create(CreatePatientRequest request)
    {
        var patient = request.ToEntity();
        await _repository.AddAsync(patient);
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PatientDto>> Update(Guid id, UpdatePatientRequest request)
    {
        var patient = await _repository.GetByIdAsync(id);
        if (patient is null) return NotFound();

        patient.ExternalId = request.ExternalId;
        patient.FirstName = request.FirstName;
        patient.LastName = request.LastName;
        patient.DateOfBirth = request.DateOfBirth;

        await _repository.UpdateAsync(patient);
        return Ok(patient.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var patient = await _repository.GetByIdAsync(id);
        if (patient is null) return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }
}
