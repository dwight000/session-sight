using Microsoft.AspNetCore.Mvc;
using SessionSight.Agents.Orchestration;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

/// <summary>
/// Controller for ingesting documents from external sources (blob trigger, etc.).
/// </summary>
[ApiController]
[Route("api/ingestion")]
public class IngestionController : ControllerBase
{
    private readonly IPatientRepository _patientRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IExtractionOrchestrator _orchestrator;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(
        IPatientRepository patientRepository,
        ISessionRepository sessionRepository,
        IExtractionOrchestrator orchestrator,
        ILogger<IngestionController> logger)
    {
        _patientRepository = patientRepository;
        _sessionRepository = sessionRepository;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Processes a note from blob storage. Creates session and triggers extraction.
    /// Called by blob trigger function when a new file is dropped.
    /// </summary>
    /// <param name="request">The processing request with blob details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Accepted response with session ID.</returns>
    [HttpPost("process")]
    [ProducesResponseType(typeof(ProcessNoteResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProcessNoteResponse>> ProcessNote(
        [FromBody] ProcessNoteRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId))
        {
            return BadRequest("PatientId is required");
        }

        if (string.IsNullOrWhiteSpace(request.BlobUri))
        {
            return BadRequest("BlobUri is required");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest("FileName is required");
        }

        _logger.LogInformation(
            "Processing note for patient {PatientId}, file: {FileName}",
            request.PatientId, request.FileName);

        // 1. Find or create patient
        var patient = await _patientRepository.GetByExternalIdAsync(request.PatientId);
        if (patient is null)
        {
            _logger.LogInformation("Creating new patient with ExternalId: {ExternalId}", request.PatientId);
            patient = await _patientRepository.AddAsync(new Patient
            {
                ExternalId = request.PatientId,
                FirstName = "Unknown",
                LastName = "Patient"
            });
        }

        // 2. Create session with document reference
        var session = new Session
        {
            PatientId = patient.Id,
            SessionDate = request.SessionDate,
            Document = new SessionDocument
            {
                Id = Guid.NewGuid(),
                BlobUri = request.BlobUri,
                OriginalFileName = request.FileName,
                Status = DocumentStatus.Pending,
                ContentType = GetContentType(request.FileName),
                UploadedAt = DateTime.UtcNow
            }
        };

        session = await _sessionRepository.AddAsync(session);
        _logger.LogInformation("Created session {SessionId} for patient {PatientId}", session.Id, patient.Id);

        // 3. Trigger extraction asynchronously (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting background extraction for session {SessionId}", session.Id);
                await _orchestrator.ProcessSessionAsync(session.Id, CancellationToken.None);
                _logger.LogInformation("Background extraction completed for session {SessionId}", session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background extraction failed for session {SessionId}", session.Id);
            }
        });

        return Accepted(new ProcessNoteResponse(
            session.Id,
            "Processing started. Use GET /api/sessions/{sessionId}/extraction to check status."
        ));
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".rtf" => "application/rtf",
            _ => "application/octet-stream"
        };
    }
}
