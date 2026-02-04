using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Models;
using SessionSight.Core.Entities;
using SessionSight.Infrastructure.Search;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;

namespace SessionSight.Agents.Services;

/// <summary>
/// Service for indexing session data in Azure AI Search for semantic search.
/// </summary>
public interface ISessionIndexingService
{
    /// <summary>
    /// Indexes a session with its extraction data and optional summary for semantic search.
    /// </summary>
    Task IndexSessionAsync(
        Session session,
        AgentExtractionResult extraction,
        SessionSummary? summary,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation that generates embeddings and indexes sessions in Azure AI Search.
/// </summary>
public partial class SessionIndexingService : ISessionIndexingService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchIndexService _searchIndexService;
    private readonly ILogger<SessionIndexingService> _logger;

    public SessionIndexingService(
        IEmbeddingService embeddingService,
        ISearchIndexService searchIndexService,
        ILogger<SessionIndexingService> logger)
    {
        _embeddingService = embeddingService;
        _searchIndexService = searchIndexService;
        _logger = logger;
    }

    public async Task IndexSessionAsync(
        Session session,
        AgentExtractionResult extraction,
        SessionSummary? summary,
        CancellationToken ct = default)
    {
        LogIndexingSession(_logger, session.Id);

        // Compose the text for embedding
        var embeddingText = ComposeEmbeddingText(session, extraction, summary);

        if (string.IsNullOrWhiteSpace(embeddingText))
        {
            LogEmptyContentSkipped(_logger, session.Id);
            return;
        }

        // Generate embedding
        var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, ct);

        // Build search document
        var searchDocument = BuildSearchDocument(session, extraction, summary, embedding);

        // Index the document
        await _searchIndexService.IndexDocumentAsync(searchDocument, ct);

        LogSessionIndexed(_logger, session.Id, embedding.Length);
    }

    /// <summary>
    /// Composes the text to be embedded from extraction data and summary.
    /// Format optimized for semantic search relevance.
    /// </summary>
    private static string ComposeEmbeddingText(
        Session session,
        AgentExtractionResult extraction,
        SessionSummary? summary)
    {
        var sb = new StringBuilder();
        var data = extraction.Data;

        // Session type and date
        var sessionType = data.SessionInfo.SessionType.Value.ToString();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Session: {sessionType} on {session.SessionDate:yyyy-MM-dd}");

        // Presenting concerns
        var primaryConcern = data.PresentingConcerns.PrimaryConcern.Value;
        if (!string.IsNullOrWhiteSpace(primaryConcern))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Concerns: {primaryConcern}");
        }

        // Secondary concerns
        var secondaryConcerns = data.PresentingConcerns.SecondaryConcerns.Value;
        if (secondaryConcerns?.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Additional concerns: {string.Join(", ", secondaryConcerns)}");
        }

        // Interventions/techniques used
        var techniques = data.Interventions.TechniquesUsed.Value;
        if (techniques?.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Interventions: {string.Join(", ", techniques)}");
        }

        // Mood score
        var moodScore = data.MoodAssessment.SelfReportedMood.Value;
        if (moodScore > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Mood: {moodScore}/10");
        }

        // Diagnosis
        var diagnosis = data.Diagnoses.PrimaryDiagnosis.Value;
        if (!string.IsNullOrWhiteSpace(diagnosis))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Diagnoses: {diagnosis}");
        }

        // Progress rating
        var progress = data.TreatmentProgress.ProgressRatingOverall.Value.ToString();
        if (progress != "None" && progress != "0")
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Progress: {progress}");
        }

        // Summary key points
        if (summary != null && !string.IsNullOrWhiteSpace(summary.KeyPoints))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Summary: {summary.KeyPoints}");
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Builds the search document from session data.
    /// </summary>
    private static SessionSearchDocument BuildSearchDocument(
        Session session,
        AgentExtractionResult extraction,
        SessionSummary? summary,
        float[] embedding)
    {
        var data = extraction.Data;

        // Extract interventions list (must be empty list, not null, for Azure Search)
        var interventions = data.Interventions.TechniquesUsed.Value?
            .Select(t => t.ToString())
            .ToList() ?? [];

        // Determine risk level
        var riskLevel = data.RiskAssessment.RiskLevelOverall.Value.ToString();

        return new SessionSearchDocument
        {
            Id = session.Id.ToString(),
            SessionId = session.Id.ToString(),
            PatientId = session.PatientId.ToString(),
            SessionDate = new DateTimeOffset(session.SessionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            SessionType = data.SessionInfo.SessionType.Value.ToString(),
            Content = data.PresentingConcerns.PrimaryConcern.Value,
            Summary = summary?.KeyPoints,
            PrimaryDiagnosis = data.Diagnoses.PrimaryDiagnosis.Value,
            Interventions = interventions,
            RiskLevel = riskLevel,
            MoodScore = data.MoodAssessment.SelfReportedMood.Value > 0
                ? data.MoodAssessment.SelfReportedMood.Value
                : null,
            ContentVector = embedding.Length > 0 ? embedding : null
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexing session {SessionId} for search")]
    private static partial void LogIndexingSession(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session {SessionId} has no content to index, skipping")]
    private static partial void LogEmptyContentSkipped(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} indexed with {VectorDimensions}-dim embedding")]
    private static partial void LogSessionIndexed(ILogger logger, Guid sessionId, int vectorDimensions);
}
