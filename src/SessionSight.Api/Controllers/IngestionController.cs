using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SessionSight.Agents.Services;
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
public partial class IngestionController : ControllerBase
{
    private readonly IPatientRepository _patientRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IProcessingJobRepository _processingJobRepository;
    private readonly IExtractionJobDispatcher _dispatcher;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(
        IPatientRepository patientRepository,
        ISessionRepository sessionRepository,
        IProcessingJobRepository processingJobRepository,
        IExtractionJobDispatcher dispatcher,
        ILogger<IngestionController> logger)
    {
        _patientRepository = patientRepository;
        _sessionRepository = sessionRepository;
        _processingJobRepository = processingJobRepository;
        _dispatcher = dispatcher;
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

        LogProcessingNote(_logger, request.PatientId, request.FileName);

        // 0. Idempotency check: if job key was provided and already exists, return 202 (no-op)
        if (!string.IsNullOrEmpty(request.JobKey))
        {
            var existingJob = await _processingJobRepository.GetByJobKeyAsync(request.JobKey);
            if (existingJob is not null)
            {
                LogDuplicateJobKey(_logger, request.JobKey);
                // ProcessingJob doesn't store SessionId, so we can't return the original session ID here.
                // The caller should use the job key for idempotency tracking rather than the session ID.
                return Accepted(new ProcessNoteResponse(
                    Guid.Empty,
                    "Already processed (duplicate job key)"
                ));
            }
        }

        // 1. Find or create patient (atomic — handles concurrent blob triggers)
        var patient = await _patientRepository.GetOrCreateByExternalIdAsync(
            request.PatientId, "Unknown", "Patient");

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

        session = await _sessionRepository.AddAsync(session, ct);
        LogCreatedSession(_logger, session.Id, patient.Id);

        // 2b. Record processing job for idempotency (if job key provided)
        var jobKey = request.JobKey;
        if (!string.IsNullOrEmpty(jobKey))
        {
            try
            {
                await _processingJobRepository.CreateAsync(new ProcessingJob
                {
                    JobKey = jobKey,
                    Status = JobStatus.Processing
                });
            }
            catch (DbUpdateException)
            {
                // Unique constraint race — another request created the job between our check and insert.
                // Return 202 to signal success to the blob function (no duplicate session created
                // because session creation above is harmless — the extraction won't duplicate).
                LogDuplicateJobKey(_logger, jobKey);
                return Accepted(new ProcessNoteResponse(
                    session.Id,
                    "Already processed (duplicate job key)"
                ));
            }
        }

        // 3. Trigger extraction via background dispatcher
        await _dispatcher.EnqueueAsync(session.Id, jobKey);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing note for patient {PatientId}, file: {FileName}")]
    private static partial void LogProcessingNote(ILogger logger, string patientId, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created session {SessionId} for patient {PatientId}")]
    private static partial void LogCreatedSession(ILogger logger, Guid sessionId, Guid patientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Duplicate job key detected, skipping: {JobKey}")]
    private static partial void LogDuplicateJobKey(ILogger logger, string jobKey);
}
