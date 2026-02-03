using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

/// <summary>
/// API endpoints for retrieving summaries at session, patient, and practice levels.
/// </summary>
[ApiController]
[Route("api/summary")]
public class SummaryController : ControllerBase
{
    private readonly ISummarizerAgent _summarizerAgent;
    private readonly ISessionRepository _sessionRepository;
    private readonly IPatientRepository _patientRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SummaryController(
        ISummarizerAgent summarizerAgent,
        ISessionRepository sessionRepository,
        IPatientRepository patientRepository)
    {
        _summarizerAgent = summarizerAgent;
        _sessionRepository = sessionRepository;
        _patientRepository = patientRepository;
    }

    /// <summary>
    /// Gets the summary for a specific session.
    /// Returns the stored summary if available, or regenerates it on demand.
    /// </summary>
    [HttpGet("session/{sessionId:guid}")]
    public async Task<ActionResult<SessionSummary>> GetSessionSummary(
        Guid sessionId,
        [FromQuery] bool regenerate = false,
        CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null)
        {
            return NotFound($"Session {sessionId} not found");
        }

        if (session.Extraction is null)
        {
            return BadRequest("Session has no extraction data. Please process the document first.");
        }

        // Return stored summary if available and not regenerating
        if (!regenerate && !string.IsNullOrEmpty(session.Extraction.SummaryJson))
        {
            var stored = JsonSerializer.Deserialize<SessionSummary>(session.Extraction.SummaryJson, JsonOptions);
            if (stored != null)
            {
                return Ok(stored);
            }
        }

        // Generate new summary
        var agentExtraction = new SessionSight.Agents.Models.ExtractionResult
        {
            SessionId = sessionId.ToString("D"),
            Data = session.Extraction.Data,
            OverallConfidence = session.Extraction.OverallConfidence,
            RequiresReview = session.Extraction.RequiresReview
        };

        var summary = await _summarizerAgent.SummarizeSessionAsync(agentExtraction, ct);

        // Store the new summary
        var summaryJson = JsonSerializer.Serialize(summary, JsonOptions);
        await _sessionRepository.UpdateExtractionSummaryAsync(session.Extraction.Id, summaryJson);

        return Ok(summary);
    }

    /// <summary>
    /// Gets a longitudinal summary for a patient across their sessions.
    /// </summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<ActionResult<PatientSummary>> GetPatientSummary(
        Guid patientId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId);
        if (patient is null)
        {
            return NotFound($"Patient {patientId} not found");
        }

        var summary = await _summarizerAgent.SummarizePatientAsync(patientId, startDate, endDate, ct);

        return Ok(summary);
    }

    /// <summary>
    /// Gets a practice-level summary aggregating metrics across all patients and sessions.
    /// </summary>
    [HttpGet("practice")]
    public async Task<ActionResult<PracticeSummary>> GetPracticeSummary(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct = default)
    {
        if (startDate > endDate)
        {
            return BadRequest("startDate must be before or equal to endDate");
        }

        var summary = await _summarizerAgent.SummarizePracticeAsync(startDate, endDate, ct);

        return Ok(summary);
    }
}
