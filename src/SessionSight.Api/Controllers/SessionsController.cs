using Microsoft.AspNetCore.Mvc;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api")]
public class SessionsController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(ISessionRepository sessionRepository, ILogger<SessionsController> logger)
    {
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<SessionDto>> GetById(Guid id)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session is null) return NotFound();
        return Ok(session.ToDto());
    }

    [HttpGet("patients/{patientId:guid}/sessions")]
    public async Task<ActionResult<IEnumerable<SessionDto>>> GetByPatientId(Guid patientId)
    {
        var sessions = await _sessionRepository.GetByPatientIdAsync(patientId);
        return Ok(sessions.Select(s => s.ToDto()));
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<SessionDto>> Create(CreateSessionRequest request)
    {
        var session = request.ToEntity();
        await _sessionRepository.AddAsync(session);
        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session.ToDto());
    }

    [HttpPut("sessions/{id:guid}")]
    public async Task<ActionResult<SessionDto>> Update(Guid id, UpdateSessionRequest request)
    {
        var session = await _sessionRepository.GetByIdAsync(id);
        if (session is null) return NotFound();

        session.TherapistId = request.TherapistId;
        session.SessionDate = request.SessionDate;
        session.SessionType = request.SessionType;
        session.Modality = request.Modality;
        session.DurationMinutes = request.DurationMinutes;
        session.SessionNumber = request.SessionNumber;

        await _sessionRepository.UpdateAsync(session);
        return Ok(session.ToDto());
    }
}
