using Microsoft.AspNetCore.Mvc;
using SessionSight.Agents.Orchestration;
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
    private readonly IExtractionOrchestrator _orchestrator;
    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<ExtractionController> _logger;

    public ExtractionController(
        IExtractionOrchestrator orchestrator,
        ISessionRepository sessionRepository,
        IDocumentRepository documentRepository,
        ILogger<ExtractionController> logger)
    {
        _orchestrator = orchestrator;
        _sessionRepository = sessionRepository;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Triggers extraction processing for a session's uploaded document.
    /// </summary>
    /// <param name="sessionId">The session ID with an uploaded document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The orchestration result with extraction status.</returns>
    [HttpPost("{sessionId:guid}")]
    [ProducesResponseType(typeof(OrchestrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrchestrationResult>> TriggerExtraction(
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

        // Use a dedicated timeout instead of the HTTP request's CancellationToken.
        // The extraction pipeline should run to completion even if the client disconnects.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var result = await _orchestrator.ProcessSessionAsync(sessionId, cts.Token);

        if (!result.Success)
        {
            LogExtractionFailed(_logger, sessionId, result.ErrorMessage);
        }

        return Ok(result);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Triggering extraction for session {SessionId}")]
    private static partial void LogTriggeringExtraction(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extraction failed for session {SessionId}: {Error}")]
    private static partial void LogExtractionFailed(ILogger logger, Guid sessionId, string? error);
}
