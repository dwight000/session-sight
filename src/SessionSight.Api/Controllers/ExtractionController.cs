using Microsoft.AspNetCore.Mvc;
using SessionSight.Agents.Services;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

/// <summary>
/// Controller for triggering and managing document extraction.
/// </summary>
[ApiController]
[Route("api/extraction")]
public partial class ExtractionController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExtractionJobDispatcher _dispatcher;
    private readonly ILogger<ExtractionController> _logger;

    public ExtractionController(
        ISessionRepository sessionRepository,
        IDocumentRepository documentRepository,
        IExtractionJobDispatcher dispatcher,
        ILogger<ExtractionController> logger)
    {
        _sessionRepository = sessionRepository;
        _documentRepository = documentRepository;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Triggers extraction processing for a session's uploaded document.
    /// Returns 202 Accepted immediately; processing runs in the background.
    /// Poll GET /api/sessions/{id}/extraction/steps for progress.
    /// </summary>
    [HttpPost("{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerExtraction(
        Guid sessionId,
        CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            return NotFound($"Session {sessionId} not found");
        }

        if (session.Document is null)
        {
            return BadRequest("Session has no document uploaded");
        }

        // Atomic transition: only one caller can move Pending/Failed/PartiallyCompleted → Processing
        var transitioned = await _documentRepository.TryTransitionDocumentStatusAsync(
                sessionId, DocumentStatus.Pending, DocumentStatus.Processing, ct)
            || await _documentRepository.TryTransitionDocumentStatusAsync(
                sessionId, DocumentStatus.Failed, DocumentStatus.Processing, ct)
            || await _documentRepository.TryTransitionDocumentStatusAsync(
                sessionId, DocumentStatus.PartiallyCompleted, DocumentStatus.Processing, ct);
        if (!transitioned)
        {
            return Conflict("Extraction already in progress or completed");
        }

        LogTriggeringExtraction(_logger, sessionId);

        await _dispatcher.EnqueueAsync(sessionId);

        return Accepted(new { sessionId });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Triggering extraction for session {SessionId}")]
    private static partial void LogTriggeringExtraction(ILogger logger, Guid sessionId);
}
