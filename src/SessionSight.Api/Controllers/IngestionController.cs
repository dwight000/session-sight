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
public partial class IngestionController : ControllerBase
{
    private readonly IPatientRepository _patientRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(
        IPatientRepository patientRepository,
        ISessionRepository sessionRepository,
        IServiceScopeFactory scopeFactory,
        ILogger<IngestionController> logger)
    {
        _patientRepository = patientRepository;
        _sessionRepository = sessionRepository;
        _scopeFactory = scopeFactory;
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

        // 1. Find or create patient
        var patient = await _patientRepository.GetByExternalIdAsync(request.PatientId);
        if (patient is null)
        {
            LogCreatingPatient(_logger, request.PatientId);
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
        LogCreatedSession(_logger, session.Id, patient.Id);

        // 3. Trigger extraction asynchronously (fire-and-forget)
        // Use IServiceScopeFactory to create a fresh DI scope that outlives this HTTP request
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IExtractionOrchestrator>();
                LogStartingBackgroundExtraction(_logger, session.Id);
                await orchestrator.ProcessSessionAsync(session.Id, CancellationToken.None);
                LogBackgroundExtractionCompleted(_logger, session.Id);
            }
            catch (Exception ex)
            {
                LogBackgroundExtractionFailed(_logger, ex, session.Id);
            }
        }, CancellationToken.None);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating new patient with ExternalId: {ExternalId}")]
    private static partial void LogCreatingPatient(ILogger logger, string externalId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created session {SessionId} for patient {PatientId}")]
    private static partial void LogCreatedSession(ILogger logger, Guid sessionId, Guid patientId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting background extraction for session {SessionId}")]
    private static partial void LogStartingBackgroundExtraction(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Background extraction completed for session {SessionId}")]
    private static partial void LogBackgroundExtractionCompleted(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Background extraction failed for session {SessionId}")]
    private static partial void LogBackgroundExtractionFailed(ILogger logger, Exception ex, Guid sessionId);
}
