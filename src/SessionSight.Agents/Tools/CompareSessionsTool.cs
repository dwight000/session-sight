using System.Globalization;
using System.Text.Json;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that compares two or more sessions side-by-side across key clinical dimensions.
/// Used by the Q&amp;A agent when asked to compare, contrast, or show differences between sessions.
/// </summary>
public class CompareSessionsTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISessionRepository _repository;

    public CompareSessionsTool(ISessionRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// When set, restricts results to sessions belonging to this patient.
    /// Returns "not found" for sessions belonging to other patients.
    /// </summary>
    public Guid? AllowedPatientId { get; set; }

    public string Name => "compare_sessions";

    public string Description => "Compare two or more sessions side-by-side across key clinical dimensions.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "sessionIds": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Two or more session IDs (GUIDs) to compare"
                }
            },
            "required": ["sessionIds"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<CompareSessionsInput>(input.ToStream(), JsonOptions, ct);

            if (request?.SessionIds is null || request.SessionIds.Count < 2)
            {
                return ToolResult.Error("At least 2 session IDs are required for comparison");
            }

            var sessionSnapshots = new List<ComparedSession>();

            foreach (var idString in request.SessionIds)
            {
                if (!Guid.TryParse(idString, out var sessionGuid))
                {
                    return ToolResult.Error($"Invalid sessionId format: {idString} - must be a valid GUID");
                }

                var session = await _repository.GetByIdAsync(sessionGuid, ct);
                if (session is null)
                {
                    return ToolResult.Error($"Session not found: {idString}");
                }

                if (AllowedPatientId.HasValue && session.PatientId != AllowedPatientId.Value)
                {
                    return ToolResult.Error($"Session not found: {idString}");
                }

                var snapshot = new ComparedSession
                {
                    SessionId = session.Id.ToString("D", CultureInfo.InvariantCulture),
                    SessionDate = session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    SessionNumber = session.SessionNumber,
                    SessionType = session.SessionType.ToString()
                };

                if (session.Extraction?.Data is not null)
                {
                    var data = session.Extraction.Data;
                    snapshot.MoodScore = data.MoodAssessment?.SelfReportedMood?.Value;
                    snapshot.MoodChange = data.MoodAssessment?.MoodChangeFromLast?.Value.ToString();
                    snapshot.RiskLevel = data.RiskAssessment?.RiskLevelOverall?.Value.ToString();
                    snapshot.PrimaryConcern = data.PresentingConcerns?.PrimaryConcern?.Value;
                    snapshot.Interventions = data.Interventions?.TechniquesUsed?.Value?
                        .Select(t => t.ToString()).ToList();
                    snapshot.NextSessionFocus = data.NextSteps?.NextSessionFocus?.Value;
                }

                sessionSnapshots.Add(snapshot);
            }

            // Build changes summary by comparing consecutive sessions (sorted by date)
            var sorted = sessionSnapshots.OrderBy(s => s.SessionDate).ToList();
            var changes = new List<string>();

            for (var i = 1; i < sorted.Count; i++)
            {
                var prev = sorted[i - 1];
                var curr = sorted[i];
                var prefix = $"Session {prev.SessionNumber}→{curr.SessionNumber}";

                if (prev.MoodScore.HasValue && curr.MoodScore.HasValue && prev.MoodScore != curr.MoodScore)
                {
                    changes.Add($"{prefix}: mood {prev.MoodScore}→{curr.MoodScore}");
                }

                if (prev.RiskLevel != curr.RiskLevel && curr.RiskLevel is not null)
                {
                    changes.Add($"{prefix}: risk {prev.RiskLevel ?? "unknown"}→{curr.RiskLevel}");
                }
            }

            var output = new CompareSessionsOutput
            {
                Sessions = sorted,
                Changes = changes
            };

            return ToolResult.Ok(output);
        }
        catch (JsonException ex)
        {
            return ToolResult.Error($"Invalid JSON input: {ex.Message}");
        }
    }
}

internal sealed class CompareSessionsInput
{
    public List<string>? SessionIds { get; set; }
}

internal sealed class ComparedSession
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionDate { get; set; } = string.Empty;
    public int SessionNumber { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public int? MoodScore { get; set; }
    public string? MoodChange { get; set; }
    public string? RiskLevel { get; set; }
    public string? PrimaryConcern { get; set; }
    public List<string>? Interventions { get; set; }
    public string? NextSessionFocus { get; set; }
}

internal sealed class CompareSessionsOutput
{
    public List<ComparedSession> Sessions { get; set; } = [];
    public List<string> Changes { get; set; } = [];
}
