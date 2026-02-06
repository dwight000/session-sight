using System.Globalization;
using System.Text.Json;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that retrieves a chronological timeline of therapy sessions for a patient.
/// Detects changes in risk level and mood between sessions.
/// </summary>
public class GetPatientTimelineTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISessionRepository _repository;

    public GetPatientTimelineTool(ISessionRepository repository)
    {
        _repository = repository;
    }

    public string Name => "get_patient_timeline";

    public string Description => "Get a chronological timeline of therapy sessions for a patient, including changes in risk level and mood between sessions.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "patientId": {
                    "type": "string",
                    "description": "The patient ID (GUID) to look up"
                },
                "startDate": {
                    "type": "string",
                    "description": "Optional start date filter (yyyy-MM-dd)"
                },
                "endDate": {
                    "type": "string",
                    "description": "Optional end date filter (yyyy-MM-dd)"
                }
            },
            "required": ["patientId"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<GetPatientTimelineInput>(input.ToStream(), JsonOptions, ct);

            if (string.IsNullOrEmpty(request?.PatientId))
            {
                return ToolResult.Error("Missing required 'patientId' parameter");
            }

            if (!Guid.TryParse(request.PatientId, out var patientGuid))
            {
                return ToolResult.Error("Invalid patientId format - must be a valid GUID");
            }

            // Use date range repo method if dates provided, else get all
            DateOnly? startDate = null;
            DateOnly? endDate = null;

            if (!string.IsNullOrEmpty(request.StartDate))
            {
                if (!DateOnly.TryParseExact(request.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
                {
                    return ToolResult.Error("Invalid startDate format - must be yyyy-MM-dd");
                }
                startDate = sd;
            }

            if (!string.IsNullOrEmpty(request.EndDate))
            {
                if (!DateOnly.TryParseExact(request.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed))
                {
                    return ToolResult.Error("Invalid endDate format - must be yyyy-MM-dd");
                }
                endDate = ed;
            }

            IEnumerable<Core.Entities.Session> sessions;
            if (startDate.HasValue || endDate.HasValue)
            {
                sessions = await _repository.GetByPatientIdInDateRangeAsync(patientGuid, startDate, endDate);
            }
            else
            {
                sessions = await _repository.GetByPatientIdAsync(patientGuid);
            }

            var ordered = sessions.OrderBy(s => s.SessionDate).ToList();

            var timeline = new List<TimelineEntry>();
            string? previousRisk = null;
            int? previousMood = null;

            foreach (var s in ordered)
            {
                var extraction = s.Extraction?.Data;
                var currentRisk = extraction?.RiskAssessment?.RiskLevelOverall?.Value.ToString();
                var currentMood = extraction?.MoodAssessment?.SelfReportedMood?.Value;

                var entry = new TimelineEntry
                {
                    SessionId = s.Id.ToString("D", CultureInfo.InvariantCulture),
                    SessionDate = s.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    SessionType = s.SessionType.ToString(),
                    SessionNumber = s.SessionNumber,
                    RiskLevel = currentRisk,
                    MoodScore = currentMood
                };

                if (previousRisk is not null && currentRisk is not null && previousRisk != currentRisk)
                {
                    entry.RiskChange = $"{previousRisk} -> {currentRisk}";
                }

                if (previousMood.HasValue && currentMood.HasValue && previousMood.Value != currentMood.Value)
                {
                    entry.MoodChange = currentMood.Value > previousMood.Value ? "improved" : "declined";
                }

                timeline.Add(entry);
                previousRisk = currentRisk;
                previousMood = currentMood;
            }

            var output = new PatientTimelineOutput
            {
                PatientId = request.PatientId,
                TotalSessions = timeline.Count,
                Timeline = timeline
            };

            if (timeline.Count > 0)
            {
                output.DateRange = $"{timeline[0].SessionDate} to {timeline[^1].SessionDate}";
            }

            return ToolResult.Ok(output);
        }
        catch (JsonException ex)
        {
            return ToolResult.Error($"Invalid JSON input: {ex.Message}");
        }
    }
}

internal sealed class GetPatientTimelineInput
{
    public string? PatientId { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

internal sealed class PatientTimelineOutput
{
    public string PatientId { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public string? DateRange { get; set; }
    public List<TimelineEntry> Timeline { get; set; } = [];
}

internal sealed class TimelineEntry
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionDate { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public int SessionNumber { get; set; }
    public string? RiskLevel { get; set; }
    public int? MoodScore { get; set; }
    public string? RiskChange { get; set; }
    public string? MoodChange { get; set; }
}
