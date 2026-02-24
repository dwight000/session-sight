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
    private readonly ISessionRepository _sessionRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtractionController> _logger;

    public ExtractionController(
        ISessionRepository sessionRepository,
        IDocumentRepository documentRepository,
        IServiceScopeFactory scopeFactory,
        ILogger<ExtractionController> logger)
    {
        _sessionRepository = sessionRepository;
        _documentRepository = documentRepository;
        _scopeFactory = scopeFactory;
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

        // Fire-and-forget: run extraction in a background scope that outlives this HTTP request.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExtractionController>>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IExtractionOrchestrator>();
                LogStartingBackgroundExtraction(logger, sessionId);
                var result = await orchestrator.ProcessSessionAsync(sessionId, CancellationToken.None);

                if (!result.Success)
                    LogExtractionFailed(logger, sessionId, result.ErrorMessage);
                else
                    LogBackgroundExtractionCompleted(logger, sessionId);
            }
            catch (Exception ex)
            {
                using var failScope = _scopeFactory.CreateScope();
                var logger = failScope.ServiceProvider.GetRequiredService<ILogger<ExtractionController>>();
                LogBackgroundExtractionCrashed(logger, ex, sessionId);
            }
        }, CancellationToken.None);

        return Accepted(new { sessionId });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Triggering extraction for session {SessionId}")]
    private static partial void LogTriggeringExtraction(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting background extraction for session {SessionId}")]
    private static partial void LogStartingBackgroundExtraction(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Background extraction completed for session {SessionId}")]
    private static partial void LogBackgroundExtractionCompleted(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extraction failed for session {SessionId}: {Error}")]
    private static partial void LogExtractionFailed(ILogger logger, Guid sessionId, string? error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Background extraction crashed for session {SessionId}")]
    private static partial void LogBackgroundExtractionCrashed(ILogger logger, Exception exception, Guid sessionId);
}
