using System.Globalization;
using System.Text.Json;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that retrieves detailed information about a specific therapy session.
/// Used by the Q&amp;A agent to drill into individual sessions.
/// </summary>
public class GetSessionDetailTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISessionRepository _repository;

    public GetSessionDetailTool(ISessionRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// When set, restricts results to sessions belonging to this patient.
    /// Returns "not found" for sessions belonging to other patients.
    /// </summary>
    public Guid? AllowedPatientId { get; set; }

    public string Name => "get_session_detail";

    public string Description => "Get detailed information about a specific therapy session including extraction data, mood, risk, diagnoses, and interventions.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "sessionId": {
                    "type": "string",
                    "description": "The session ID (GUID) to retrieve"
                }
            },
            "required": ["sessionId"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<GetSessionDetailInput>(input.ToStream(), JsonOptions, ct);

            if (string.IsNullOrEmpty(request?.SessionId))
            {
                return ToolResult.Error("Missing required 'sessionId' parameter");
            }

            if (!Guid.TryParse(request.SessionId, out var sessionGuid))
            {
                return ToolResult.Error("Invalid sessionId format - must be a valid GUID");
            }

            var session = await _repository.GetByIdAsync(sessionGuid);
            if (session is null)
            {
                return ToolResult.Error($"Session not found: {request.SessionId}");
            }

            if (AllowedPatientId.HasValue && session.PatientId != AllowedPatientId.Value)
            {
                return ToolResult.Error($"Session not found: {request.SessionId}");
            }

            var extraction = session.Extraction;
            var output = new SessionDetailOutput
            {
                SessionId = session.Id.ToString("D", CultureInfo.InvariantCulture),
                SessionDate = session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                SessionType = session.SessionType.ToString(),
                SessionNumber = session.SessionNumber,
                DurationMinutes = session.DurationMinutes,
                HasExtraction = extraction is not null
            };

            if (extraction?.Data is not null)
            {
                output.OverallConfidence = extraction.OverallConfidence;
                output.MoodScore = extraction.Data.MoodAssessment?.SelfReportedMood?.Value;
                output.MoodChange = extraction.Data.MoodAssessment?.MoodChangeFromLast?.Value.ToString();
                output.RiskLevel = extraction.Data.RiskAssessment?.RiskLevelOverall?.Value.ToString();
                output.PrimaryDiagnosis = extraction.Data.Diagnoses?.PrimaryDiagnosis?.Value;
                output.PrimaryConcern = extraction.Data.PresentingConcerns?.PrimaryConcern?.Value;

                output.Interventions = extraction.Data.Interventions?.TechniquesUsed?.Value?
                    .Select(t => t.ToString()).ToList();

                output.SecondaryConcerns = extraction.Data.PresentingConcerns?.SecondaryConcerns?.Value;
                output.NextSessionFocus = extraction.Data.NextSteps?.NextSessionFocus?.Value;
                output.SummaryJson = extraction.SummaryJson;
            }

            return ToolResult.Ok(output);
        }
        catch (JsonException ex)
        {
            return ToolResult.Error($"Invalid JSON input: {ex.Message}");
        }
    }
}

internal sealed class GetSessionDetailInput
{
    public string? SessionId { get; set; }
}

internal sealed class SessionDetailOutput
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionDate { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public int SessionNumber { get; set; }
    public int? DurationMinutes { get; set; }
    public bool HasExtraction { get; set; }
    public double? OverallConfidence { get; set; }
    public int? MoodScore { get; set; }
    public string? MoodChange { get; set; }
    public string? RiskLevel { get; set; }
    public string? PrimaryDiagnosis { get; set; }
    public string? PrimaryConcern { get; set; }
    public List<string>? Interventions { get; set; }
    public List<string>? SecondaryConcerns { get; set; }
    public string? NextSessionFocus { get; set; }
    public string? SummaryJson { get; set; }
}
