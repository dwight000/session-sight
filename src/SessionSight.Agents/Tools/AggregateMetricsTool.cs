using System.Globalization;
using System.Text.Json;
using SessionSight.Core.Interfaces;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that computes aggregate metrics from a patient's therapy sessions.
/// Supports mood trends, session counts, intervention frequency, risk distribution, and diagnosis history.
/// </summary>
public class AggregateMetricsTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISessionRepository _repository;

    public AggregateMetricsTool(ISessionRepository repository)
    {
        _repository = repository;
    }

    public string Name => "aggregate_metrics";

    public string Description => "Compute aggregate metrics from a patient's therapy sessions. Supports: mood_trend, session_count, intervention_frequency, risk_distribution, diagnosis_history.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "patientId": {
                    "type": "string",
                    "description": "The patient ID (GUID) to compute metrics for"
                },
                "metricType": {
                    "type": "string",
                    "enum": ["mood_trend", "session_count", "intervention_frequency", "risk_distribution", "diagnosis_history"],
                    "description": "The type of metric to compute"
                }
            },
            "required": ["patientId", "metricType"]
        }
        """);

    public async Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<AggregateMetricsInput>(input.ToStream(), JsonOptions, ct);

            if (string.IsNullOrEmpty(request?.PatientId))
            {
                return ToolResult.Error("Missing required 'patientId' parameter");
            }

            if (!Guid.TryParse(request.PatientId, out var patientGuid))
            {
                return ToolResult.Error("Invalid patientId format - must be a valid GUID");
            }

            if (string.IsNullOrEmpty(request.MetricType))
            {
                return ToolResult.Error("Missing required 'metricType' parameter");
            }

            var sessions = (await _repository.GetByPatientIdAsync(patientGuid))
                .OrderBy(s => s.SessionDate)
                .ToList();

            return request.MetricType switch
            {
                "mood_trend" => ComputeMoodTrend(sessions),
                "session_count" => ComputeSessionCount(sessions),
                "intervention_frequency" => ComputeInterventionFrequency(sessions),
                "risk_distribution" => ComputeRiskDistribution(sessions),
                "diagnosis_history" => ComputeDiagnosisHistory(sessions),
                _ => ToolResult.Error($"Invalid metricType '{request.MetricType}'. Must be one of: mood_trend, session_count, intervention_frequency, risk_distribution, diagnosis_history")
            };
        }
        catch (JsonException ex)
        {
            return ToolResult.Error($"Invalid JSON input: {ex.Message}");
        }
    }

    private static string GetTrendDirection(List<double> scores)
    {
        if (scores.Count < 2) return "insufficient_data";
        if (scores[^1] > scores[0]) return "improving";
        if (scores[^1] < scores[0]) return "declining";
        return "stable";
    }

    private static ToolResult ComputeMoodTrend(List<Core.Entities.Session> sessions)
    {
        var moodScores = sessions
            .Where(s => s.Extraction?.Data?.MoodAssessment?.SelfReportedMood?.Value is > 0)
            .Select(s => new
            {
                Date = s.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Score = s.Extraction!.Data!.MoodAssessment!.SelfReportedMood!.Value
            })
            .ToList();

        if (moodScores.Count == 0)
        {
            return ToolResult.Ok(new { metricType = "mood_trend", dataPoints = 0, message = "No mood data available" });
        }

        var scores = moodScores.Select(m => (double)m.Score).ToList();
        var trend = GetTrendDirection(scores);

        return ToolResult.Ok(new
        {
            metricType = "mood_trend",
            average = Math.Round(scores.Average(), 2),
            min = scores.Min(),
            max = scores.Max(),
            trend,
            dataPoints = moodScores.Count,
            recentValues = moodScores.TakeLast(5).Select(m => new { m.Date, m.Score })
        });
    }

    private static ToolResult ComputeSessionCount(List<Core.Entities.Session> sessions)
    {
        var byType = sessions.GroupBy(s => s.SessionType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byModality = sessions.GroupBy(s => s.Modality.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        string? dateRange = null;
        if (sessions.Count > 0)
        {
            var first = sessions[0].SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var last = sessions[^1].SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            dateRange = $"{first} to {last}";
        }

        return ToolResult.Ok(new
        {
            metricType = "session_count",
            total = sessions.Count,
            byType,
            byModality,
            dateRange
        });
    }

    private static ToolResult ComputeInterventionFrequency(List<Core.Entities.Session> sessions)
    {
        var interventionCounts = new Dictionary<string, int>();

        foreach (var session in sessions)
        {
            var techniques = session.Extraction?.Data?.Interventions?.TechniquesUsed?.Value;
            if (techniques is null) continue;

            foreach (var technique in techniques)
            {
                var name = technique.ToString();
                interventionCounts.TryGetValue(name, out var count);
                interventionCounts[name] = count + 1;
            }
        }

        var sorted = interventionCounts
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new { intervention = kvp.Key, count = kvp.Value })
            .ToList();

        return ToolResult.Ok(new
        {
            metricType = "intervention_frequency",
            totalInterventionTypes = sorted.Count,
            interventions = sorted
        });
    }

    private static ToolResult ComputeRiskDistribution(List<Core.Entities.Session> sessions)
    {
        var riskCounts = sessions
            .Where(s => s.Extraction?.Data?.RiskAssessment?.RiskLevelOverall?.Value is not null)
            .GroupBy(s => s.Extraction!.Data!.RiskAssessment!.RiskLevelOverall!.Value.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return ToolResult.Ok(new
        {
            metricType = "risk_distribution",
            totalAssessed = riskCounts.Values.Sum(),
            distribution = riskCounts
        });
    }

    private static ToolResult ComputeDiagnosisHistory(List<Core.Entities.Session> sessions)
    {
        var diagnosisMap = new Dictionary<string, (string FirstSeen, string LastSeen)>();

        foreach (var session in sessions)
        {
            var diagnosis = session.Extraction?.Data?.Diagnoses?.PrimaryDiagnosis?.Value;
            if (string.IsNullOrEmpty(diagnosis)) continue;

            var date = session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (diagnosisMap.TryGetValue(diagnosis, out var existing))
            {
                diagnosisMap[diagnosis] = (existing.FirstSeen, date);
            }
            else
            {
                diagnosisMap[diagnosis] = (date, date);
            }
        }

        var diagnoses = diagnosisMap
            .Select(kvp => new { diagnosis = kvp.Key, firstSeen = kvp.Value.FirstSeen, lastSeen = kvp.Value.LastSeen })
            .ToList();

        return ToolResult.Ok(new
        {
            metricType = "diagnosis_history",
            totalDiagnoses = diagnoses.Count,
            diagnoses
        });
    }
}

internal sealed class AggregateMetricsInput
{
    public string? PatientId { get; set; }
    public string? MetricType { get; set; }
}
