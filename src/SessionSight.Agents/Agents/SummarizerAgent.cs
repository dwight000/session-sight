using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SessionSight.Agents.Helpers;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;

namespace SessionSight.Agents.Agents;

/// <summary>
/// Summarizer Agent implementation.
/// Generates session, patient, and practice-level summaries.
/// </summary>
public partial class SummarizerAgent : ISummarizerAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SummarizerAgent> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SummarizerAgent(
        IAIFoundryClientFactory clientFactory,
        IModelRouter modelRouter,
        ISessionRepository sessionRepository,
        ILogger<SummarizerAgent> logger)
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public string Name => "SummarizerAgent";

    public async Task<SessionSummary> SummarizeSessionAsync(
        AgentExtractionResult extraction,
        CancellationToken ct = default)
    {
        LogStartingSessionSummary(_logger, extraction.SessionId);

        var modelName = _modelRouter.SelectModel(ModelTask.Summarization);

        // Serialize extraction data for the prompt
        var extractionJson = JsonSerializer.Serialize(extraction.Data, JsonOptions);

        try
        {
            var chatClient = _clientFactory.CreateChatClient(modelName);
            var prompt = SummarizerPrompts.GetSessionSummaryPrompt(extractionJson);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SummarizerPrompts.SystemPrompt),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.3f,
                MaxOutputTokenCount = 1024
            };

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            var content = response.Value.Content[0].Text;

            var summary = ParseSessionSummary(content);
            summary.SessionId = Guid.Parse(extraction.SessionId);
            summary.ModelUsed = modelName;
            summary.GeneratedAt = DateTime.UtcNow;

            LogSessionSummaryCompleted(_logger, extraction.SessionId);
            return summary;
        }
        catch (Exception ex)
        {
            LogSessionSummaryError(_logger, ex, extraction.SessionId);

            // Return a minimal summary on error
            return new SessionSummary
            {
                SessionId = Guid.Parse(extraction.SessionId),
                OneLiner = "Summary generation failed",
                KeyPoints = ex.Message,
                ModelUsed = modelName,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<PatientSummary> SummarizePatientAsync(
        Guid patientId,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken ct = default)
    {
        LogStartingPatientSummary(_logger, patientId);

        var modelName = _modelRouter.SelectModel(ModelTask.Summarization);

        // Get patient's sessions with extractions
        var sessions = (await _sessionRepository.GetByPatientIdInDateRangeAsync(patientId, startDate, endDate))
            .Where(s => s.Extraction != null)
            .OrderBy(s => s.SessionDate)
            .ToList();

        if (sessions.Count == 0)
        {
            return new PatientSummary
            {
                PatientId = patientId,
                Period = new DateRange
                {
                    Start = startDate ?? DateOnly.MinValue,
                    End = endDate ?? DateOnly.MaxValue
                },
                SessionCount = 0,
                ProgressNarrative = "No sessions with extractions found for this patient in the specified period.",
                MoodTrend = MoodTrend.InsufficientData,
                ModelUsed = modelName,
                GeneratedAt = DateTime.UtcNow
            };
        }

        // Prepare session data for prompt
        var sessionData = sessions.Select(s => new
        {
            Date = s.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            s.SessionType,
            s.DurationMinutes,
            Extraction = s.Extraction?.Data
        });
        var sessionsJson = JsonSerializer.Serialize(sessionData, JsonOptions);

        try
        {
            var chatClient = _clientFactory.CreateChatClient(modelName);
            var prompt = SummarizerPrompts.GetPatientSummaryPrompt(sessionsJson, sessions.Count);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SummarizerPrompts.SystemPrompt),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.3f,
                MaxOutputTokenCount = 2048
            };

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            var content = response.Value.Content[0].Text;

            var summary = ParsePatientSummary(content);
            summary.PatientId = patientId;
            summary.Period = new DateRange
            {
                Start = sessions.Min(s => s.SessionDate),
                End = sessions.Max(s => s.SessionDate)
            };
            summary.SessionCount = sessions.Count;
            summary.ModelUsed = modelName;
            summary.GeneratedAt = DateTime.UtcNow;

            LogPatientSummaryCompleted(_logger, patientId, sessions.Count);
            return summary;
        }
        catch (Exception ex)
        {
            LogPatientSummaryError(_logger, ex, patientId);

            return new PatientSummary
            {
                PatientId = patientId,
                Period = new DateRange
                {
                    Start = sessions.Min(s => s.SessionDate),
                    End = sessions.Max(s => s.SessionDate)
                },
                SessionCount = sessions.Count,
                ProgressNarrative = $"Summary generation failed: {ex.Message}",
                MoodTrend = MoodTrend.InsufficientData,
                ModelUsed = modelName,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<PracticeSummary> SummarizePracticeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        LogStartingPracticeSummary(_logger, startDate, endDate);

        // Get all sessions in range
        var allSessions = (await _sessionRepository.GetAllInDateRangeAsync(startDate, endDate)).ToList();
        var flaggedSessions = (await _sessionRepository.GetFlaggedSessionsAsync(startDate, endDate)).ToList();

        // Aggregate metrics locally (no LLM needed for counts)
        var summary = new PracticeSummary
        {
            Period = new DateRange { Start = startDate, End = endDate },
            TotalSessions = allSessions.Count,
            TotalPatients = allSessions.Select(s => s.PatientId).Distinct().Count(),
            SessionsRequiringReview = flaggedSessions.Count,
            GeneratedAt = DateTime.UtcNow
        };

        // Calculate risk distribution
        summary.RiskDistribution = CalculateRiskDistribution(allSessions);

        // Calculate average sessions per patient
        if (summary.TotalPatients > 0)
        {
            summary.AverageSessionsPerPatient = Math.Round(
                (double)summary.TotalSessions / summary.TotalPatients, 2);
        }

        // Aggregate top interventions
        summary.TopInterventions = AggregateInterventions(allSessions);

        // Summarize flagged patients
        summary.FlaggedPatients = SummarizeFlaggedPatients(flaggedSessions);
        summary.FlaggedPatientCount = summary.FlaggedPatients.Count;

        LogPracticeSummaryCompleted(_logger, startDate, endDate, summary.TotalSessions, summary.FlaggedPatientCount);

        return summary;
    }

    internal static SessionSummary ParseSessionSummary(string content)
    {
        var json = ExtractJson(content);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            var summary = new SessionSummary();

            if (parsed.TryGetProperty("oneLiner", out var oneLiner))
                summary.OneLiner = oneLiner.GetString() ?? string.Empty;

            if (parsed.TryGetProperty("keyPoints", out var keyPoints))
                summary.KeyPoints = keyPoints.GetString() ?? string.Empty;

            if (parsed.TryGetProperty("interventionsUsed", out var interventions) &&
                interventions.ValueKind == JsonValueKind.Array)
            {
                summary.InterventionsUsed = interventions.EnumerateArray()
                    .Select(i => i.GetString())
                    .Where(s => s != null)
                    .ToList()!;
            }

            if (parsed.TryGetProperty("nextSessionFocus", out var nextFocus))
                summary.NextSessionFocus = nextFocus.GetString() ?? string.Empty;

            if (parsed.TryGetProperty("riskFlags", out var riskFlags) &&
                riskFlags.ValueKind == JsonValueKind.Object)
            {
                summary.RiskFlags = ParseRiskSummary(riskFlags);
            }

            return summary;
        }
        catch (JsonException)
        {
            return new SessionSummary
            {
                OneLiner = "Failed to parse summary response",
                KeyPoints = content
            };
        }
    }

    internal static PatientSummary ParsePatientSummary(string content)
    {
        var json = ExtractJson(content);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            return BuildPatientSummaryFromJson(parsed);
        }
        catch (JsonException)
        {
            return new PatientSummary
            {
                ProgressNarrative = "Failed to parse patient summary response",
                MoodTrend = MoodTrend.InsufficientData
            };
        }
    }

    private static PatientSummary BuildPatientSummaryFromJson(JsonElement parsed)
    {
        var summary = new PatientSummary();

        if (parsed.TryGetProperty("progressNarrative", out var narrative))
            summary.ProgressNarrative = narrative.GetString() ?? string.Empty;

        if (parsed.TryGetProperty("moodTrend", out var mood))
            summary.MoodTrend = ParseMoodTrend(mood.GetString());

        if (parsed.TryGetProperty("recurringThemes", out var themes))
            summary.RecurringThemes = ParseStringArray(themes);

        if (parsed.TryGetProperty("goalProgress", out var goals))
            summary.GoalProgress = ParseGoalProgressArray(goals);

        if (parsed.TryGetProperty("effectiveInterventions", out var effective))
            summary.EffectiveInterventions = ParseStringArray(effective);

        if (parsed.TryGetProperty("recommendedFocus", out var focus))
            summary.RecommendedFocus = focus.GetString() ?? string.Empty;

        if (parsed.TryGetProperty("riskTrendSummary", out var riskTrend))
            summary.RiskTrendSummary = riskTrend.GetString() ?? string.Empty;

        return summary;
    }

    private static MoodTrend ParseMoodTrend(string? moodStr)
    {
        if (Enum.TryParse<MoodTrend>(moodStr, ignoreCase: true, out var trend))
            return trend;
        return MoodTrend.InsufficientData;
    }

    private static List<string> ParseStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return element.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => s != null)
            .ToList()!;
    }

    private static List<GoalProgress> ParseGoalProgressArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return new List<GoalProgress>();

        return element.EnumerateArray()
            .Select(g =>
            {
                var gp = new GoalProgress();
                if (g.TryGetProperty("goal", out var goal))
                    gp.Goal = goal.GetString() ?? string.Empty;
                if (g.TryGetProperty("status", out var status))
                    gp.Status = status.GetString() ?? string.Empty;
                return gp;
            })
            .ToList();
    }

    private static RiskSummary? ParseRiskSummary(JsonElement element)
    {
        var risk = new RiskSummary();

        if (element.TryGetProperty("riskLevel", out var level))
            risk.RiskLevel = level.GetString() ?? "Low";

        if (element.TryGetProperty("flags", out var flags) &&
            flags.ValueKind == JsonValueKind.Array)
        {
            risk.Flags = flags.EnumerateArray()
                .Select(f => f.GetString())
                .Where(s => s != null)
                .ToList()!;
        }

        if (element.TryGetProperty("requiresReview", out var review))
            risk.RequiresReview = review.ValueKind == JsonValueKind.True;

        // Return null if risk is minimal
        if (risk.RiskLevel == "Low" && risk.Flags.Count == 0 && !risk.RequiresReview)
            return null;

        return risk;
    }

    internal static string ExtractJson(string content) => LlmJsonHelper.ExtractJson(content);

    private static RiskLevelBreakdown CalculateRiskDistribution(List<Session> sessions)
    {
        var breakdown = new RiskLevelBreakdown();

        foreach (var session in sessions.Where(s => s.Extraction?.Data?.RiskAssessment != null))
        {
            var riskLevel = session.Extraction!.Data.RiskAssessment.RiskLevelOverall.Value;
            switch (riskLevel)
            {
                case RiskLevelOverall.Low:
                    breakdown.Low++;
                    break;
                case RiskLevelOverall.Moderate:
                    breakdown.Moderate++;
                    break;
                case RiskLevelOverall.High:
                    breakdown.High++;
                    break;
                case RiskLevelOverall.Imminent:
                    breakdown.Imminent++;
                    break;
            }
        }

        return breakdown;
    }

    private static List<InterventionFrequency> AggregateInterventions(List<Session> sessions)
    {
        var interventionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in sessions.Where(s => s.Extraction?.Data?.Interventions != null))
        {
            var interventions = session.Extraction!.Data.Interventions;

            if (interventions.TechniquesUsed.Value != null)
            {
                foreach (var technique in interventions.TechniquesUsed.Value)
                {
                    var techniqueName = technique.ToString();
                    interventionCounts[techniqueName] = interventionCounts.GetValueOrDefault(techniqueName) + 1;
                }
            }
        }

        return interventionCounts
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new InterventionFrequency
            {
                Intervention = kv.Key,
                Count = kv.Value
            })
            .ToList();
    }

    private static List<FlaggedPatientSummary> SummarizeFlaggedPatients(List<Session> flaggedSessions)
    {
        return flaggedSessions
            .GroupBy(s => s.PatientId)
            .Select(g =>
            {
                var latestSession = g.OrderByDescending(s => s.SessionDate).First();
                var highestRisk = g
                    .Where(s => s.Extraction?.Data?.RiskAssessment != null)
                    .Max(s => s.Extraction!.Data.RiskAssessment.RiskLevelOverall.Value);

                return new FlaggedPatientSummary
                {
                    PatientId = g.Key,
                    PatientIdentifier = latestSession.Patient?.ExternalId ?? g.Key.ToString()[..8],
                    HighestRiskLevel = highestRisk.ToString(),
                    FlaggedSessionCount = g.Count(),
                    LastSessionDate = latestSession.SessionDate,
                    FlagReason = DetermineRiskReason(latestSession)
                };
            })
            .OrderByDescending(f => GetRiskSeverity(f.HighestRiskLevel))
            .ThenByDescending(f => f.FlaggedSessionCount)
            .ToList();
    }

    private static string DetermineRiskReason(Session session)
    {
        if (session.Extraction?.Data?.RiskAssessment == null)
            return "Requires clinical review";

        var risk = session.Extraction.Data.RiskAssessment;

        if (risk.SuicidalIdeation.Value > SuicidalIdeation.None)
            return $"Suicidal ideation: {risk.SuicidalIdeation.Value}";

        if (risk.SelfHarm.Value > SelfHarm.None)
            return $"Self-harm: {risk.SelfHarm.Value}";

        if (risk.HomicidalIdeation.Value > HomicidalIdeation.None)
            return $"Homicidal ideation: {risk.HomicidalIdeation.Value}";

        return "Elevated risk indicators";
    }

    private static int GetRiskSeverity(string riskLevel) => riskLevel switch
    {
        "Imminent" => 3,
        "High" => 2,
        "Moderate" => 1,
        _ => 0
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting session summary for {SessionId}")]
    private static partial void LogStartingSessionSummary(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session summary completed for {SessionId}")]
    private static partial void LogSessionSummaryCompleted(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Session summary failed for {SessionId}")]
    private static partial void LogSessionSummaryError(ILogger logger, Exception exception, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting patient summary for {PatientId}")]
    private static partial void LogStartingPatientSummary(ILogger logger, Guid patientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Patient summary completed for {PatientId}, {SessionCount} sessions")]
    private static partial void LogPatientSummaryCompleted(ILogger logger, Guid patientId, int sessionCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Patient summary failed for {PatientId}")]
    private static partial void LogPatientSummaryError(ILogger logger, Exception exception, Guid patientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting practice summary for {StartDate} to {EndDate}")]
    private static partial void LogStartingPracticeSummary(ILogger logger, DateOnly startDate, DateOnly endDate);

    [LoggerMessage(Level = LogLevel.Information, Message = "Practice summary completed for {StartDate} to {EndDate}: {TotalSessions} sessions, {FlaggedPatients} flagged patients")]
    private static partial void LogPracticeSummaryCompleted(ILogger logger, DateOnly startDate, DateOnly endDate, int totalSessions, int flaggedPatients);
}
