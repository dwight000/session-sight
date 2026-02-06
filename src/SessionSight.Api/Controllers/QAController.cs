using Microsoft.AspNetCore.Mvc;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

/// <summary>
/// API endpoints for clinical Q&amp;A powered by RAG.
/// </summary>
[ApiController]
[Route("api/qa")]
public class QAController : ControllerBase
{
    private readonly IQAAgent _qaAgent;
    private readonly IPatientRepository _patientRepository;

    public QAController(
        IQAAgent qaAgent,
        IPatientRepository patientRepository)
    {
        _qaAgent = qaAgent;
        _patientRepository = patientRepository;
    }

    /// <summary>
    /// Asks a clinical question scoped to a specific patient's sessions.
    /// </summary>
    [HttpPost("patient/{patientId:guid}")]
    public async Task<ActionResult<QAResponse>> AskAboutPatient(
        Guid patientId,
        [FromBody] QARequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question cannot be empty.");
        }

        var patient = await _patientRepository.GetByIdAsync(patientId);
        if (patient is null)
        {
            return NotFound($"Patient {patientId} not found");
        }

        var response = await _qaAgent.AnswerAsync(request.Question, patientId, ct);

        return Ok(response);
    }
}
