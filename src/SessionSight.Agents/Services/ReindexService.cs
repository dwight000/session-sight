using System.Text.Json;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Models;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;

namespace SessionSight.Agents.Services;

public record ReindexResult(int Indexed, int Failed, int Skipped);

public interface IReindexService
{
    Task<ReindexResult> ReindexSessionsAsync(
        IReadOnlyList<Session> sessions, CancellationToken ct = default);
}

public partial class ReindexService : IReindexService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISessionIndexingService _indexingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<ReindexService> _logger;

    public ReindexService(
        ISessionIndexingService indexingService,
        IDocumentRepository documentRepository,
        ILogger<ReindexService> logger)
    {
        _indexingService = indexingService;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<ReindexResult> ReindexSessionsAsync(
        IReadOnlyList<Session> sessions, CancellationToken ct = default)
    {
        var indexed = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var session in sessions)
        {
            ct.ThrowIfCancellationRequested();

            if (session.Extraction is null)
            {
                LogSkippingNoExtraction(_logger, session.Id);
                skipped++;
                continue;
            }

            try
            {
                var agentExtraction = new AgentExtractionResult
                {
                    SessionId = session.Id.ToString("D"),
                    Data = session.Extraction.Data,
                    OverallConfidence = session.Extraction.OverallConfidence,
                    RequiresReview = session.Extraction.RequiresReview
                };

                SessionSummary? summary = null;
                if (!string.IsNullOrEmpty(session.Extraction.SummaryJson))
                {
                    summary = JsonSerializer.Deserialize<SessionSummary>(
                        session.Extraction.SummaryJson, JsonOptions);
                }

                await _indexingService.IndexSessionAsync(session, agentExtraction, summary, ct);

                await _documentRepository.UpdateDocumentStatusAsync(
                    session.Id,
                    session.Document!.Status,
                    indexingStatus: IndexingStatus.Indexed,
                    ct: ct);

                LogSessionReindexed(_logger, session.Id);
                indexed++;
            }
            catch (Exception ex)
            {
                LogReindexFailed(_logger, ex, session.Id);

                try
                {
                    await _documentRepository.UpdateDocumentStatusAsync(
                        session.Id,
                        session.Document!.Status,
                        indexingStatus: IndexingStatus.Failed,
                        ct: ct);
                }
                catch (Exception updateEx)
                {
                    LogStatusUpdateFailed(_logger, updateEx, session.Id);
                }

                failed++;
            }
        }

        return new ReindexResult(indexed, failed, skipped);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping session {SessionId}: no extraction data")]
    private static partial void LogSkippingNoExtraction(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} reindexed successfully")]
    private static partial void LogSessionReindexed(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to reindex session {SessionId}")]
    private static partial void LogReindexFailed(ILogger logger, Exception ex, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update indexing status for session {SessionId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, Exception ex, Guid sessionId);
}
