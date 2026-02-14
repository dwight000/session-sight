using Microsoft.AspNetCore.Mvc;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TherapistsController : ControllerBase
{
    private readonly ITherapistRepository _repository;

    public TherapistsController(ITherapistRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TherapistDto>>> GetAll()
    {
        var therapists = await _repository.GetAllAsync();
        return Ok(therapists.Select(t => t.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TherapistDto>> GetById(Guid id)
    {
        var therapist = await _repository.GetByIdAsync(id);
        if (therapist is null) return NotFound();
        return Ok(therapist.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<TherapistDto>> Create(CreateTherapistRequest request)
    {
        var therapist = request.ToEntity();
        await _repository.AddAsync(therapist);
        return CreatedAtAction(nameof(GetById), new { id = therapist.Id }, therapist.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TherapistDto>> Update(Guid id, UpdateTherapistRequest request)
    {
        var therapist = await _repository.GetByIdAsync(id);
        if (therapist is null) return NotFound();

        therapist.Name = request.Name;
        therapist.LicenseNumber = request.LicenseNumber;
        therapist.Credentials = request.Credentials;
        therapist.IsActive = request.IsActive;

        await _repository.UpdateAsync(therapist);
        return Ok(therapist.ToDto());
    }

    [HttpDelete("{id:guid}")]
    // codeql[cs/web/missing-function-level-access-control] - Auth not yet implemented (dev environment, consistent with all controllers)
    // codeql[cs/web/insecure-direct-object-reference] - Auth not yet implemented (dev environment, consistent with all controllers)
    public async Task<IActionResult> Delete(Guid id)
    {
        var therapist = await _repository.GetByIdAsync(id);
        if (therapist is null) return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }
}
