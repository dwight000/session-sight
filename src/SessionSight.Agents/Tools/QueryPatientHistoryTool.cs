using System.Text.Json;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that queries prior session history for a patient.
/// Wraps <see cref="ISessionRepository"/>.
/// </summary>
public class QueryPatientHistoryTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISessionRepository _repository;

    public QueryPatientHistoryTool(ISessionRepository repository)
    {
        _repository = repository;
    }

    public string Name => "query_patient_history";

    public string Description => "Get prior session history for a patient. Returns summary of recent sessions including risk levels, diagnoses, and session metadata.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "patientId": {
                    "type": "string",
                    "description": "The patient ID (GUID) to look up"
                },
                "maxSessions": {
                    "type": "integer",
                    "description": "Maximum number of prior sessions to return (default 5)"
                }
            },
            "required": ["patientId"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = JsonSerializer.Deserialize<QueryPatientHistoryInput>(input.ToStream(), JsonOptions);

            if (string.IsNullOrEmpty(request?.PatientId))
            {
                return ToolResult.Error("Missing required 'patientId' parameter");
            }

            if (!Guid.TryParse(request.PatientId, out var patientGuid))
            {
                return ToolResult.Error("Invalid patientId format - must be a valid GUID");
            }

            var maxSessions = request.MaxSessions ?? 5;
            var sessions = await _repository.GetByPatientIdAsync(patientGuid);

            var summaries = sessions
                .OrderByDescending(s => s.SessionDate)
                .Take(maxSessions)
                .Select(s => new SessionSummary
                {
                    SessionId = s.Id.ToString(),
                    SessionDate = s.SessionDate.ToString("yyyy-MM-dd"),
                    SessionNumber = s.SessionNumber,
                    SessionType = s.SessionType.ToString(),
                    HasExtraction = s.Extraction is not null,
                    RiskLevel = s.Extraction?.Data?.RiskAssessment?.RiskLevelOverall?.Value.ToString(),
                    PrimaryDiagnosis = s.Extraction?.Data?.Diagnoses?.PrimaryDiagnosis?.Value
                })
                .ToList();

            return ToolResult.Ok(new QueryPatientHistoryOutput
            {
                PatientId = request.PatientId,
                SessionCount = summaries.Count,
                Sessions = summaries
            });
        }
        catch (JsonException ex)
        {
            return ToolResult.Error($"Invalid JSON input: {ex.Message}");
        }
    }
}

internal class QueryPatientHistoryInput
{
    public string? PatientId { get; set; }
    public int? MaxSessions { get; set; }
}

internal class QueryPatientHistoryOutput
{
    public string PatientId { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public List<SessionSummary> Sessions { get; set; } = [];
}

internal class SessionSummary
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionDate { get; set; } = string.Empty;
    public int SessionNumber { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public bool HasExtraction { get; set; }
    public string? RiskLevel { get; set; }
    public string? PrimaryDiagnosis { get; set; }
}
