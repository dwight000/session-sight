using Microsoft.AspNetCore.Mvc;
using SessionSight.Agents.Services;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api/admin")]
public partial class AdminController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ISessionRepository sessionRepository,
        IServiceScopeFactory scopeFactory,
        ILogger<AdminController> logger)
    {
        _sessionRepository = sessionRepository;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Triggers reindexing for sessions with Failed or None indexing status.
    /// Returns 202 immediately; reindexing runs in the background.
    /// </summary>
    [HttpPost("reindex")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Reindex(
        [FromQuery] Guid? patientId = null,
        [FromQuery] Guid? sessionId = null,
        CancellationToken ct = default)
    {
        var sessions = await _sessionRepository.GetSessionsNeedingReindexAsync(patientId, sessionId, ct);

        LogReindexQueued(_logger, sessions.Count, patientId, sessionId);

        if (sessions.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var reindexService = scope.ServiceProvider.GetRequiredService<IReindexService>();
                    var result = await reindexService.ReindexSessionsAsync(sessions);
                    LogReindexCompleted(_logger, result.Indexed, result.Failed, result.Skipped);
                }
                catch (Exception ex)
                {
                    LogReindexError(_logger, ex);
                }
            }, CancellationToken.None);
        }

        return Accepted(new { queued = sessions.Count });
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Reindex queued: {Count} sessions (patientId={PatientId}, sessionId={SessionId})")]
    private static partial void LogReindexQueued(ILogger logger, int count, Guid? patientId, Guid? sessionId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Reindex completed: {Indexed} indexed, {Failed} failed, {Skipped} skipped")]
    private static partial void LogReindexCompleted(ILogger logger, int indexed, int failed, int skipped);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reindex background task failed")]
    private static partial void LogReindexError(ILogger logger, Exception ex);
}
