using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Api.DTOs;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

/// <summary>
/// API endpoints for retrieving summaries at session, patient, and practice levels.
/// </summary>
[ApiController]
[Route("api/summary")]
public class SummaryController : ControllerBase
{
    private readonly ISummarizerAgent _summarizerAgent;
    private readonly ISessionRepository _sessionRepository;
    private readonly IPatientRepository _patientRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SummaryController(
        ISummarizerAgent summarizerAgent,
        ISessionRepository sessionRepository,
        IPatientRepository patientRepository)
    {
        _summarizerAgent = summarizerAgent;
        _sessionRepository = sessionRepository;
        _patientRepository = patientRepository;
    }

    /// <summary>
    /// Gets the summary for a specific session.
    /// Returns the stored summary if available, or regenerates it on demand.
    /// </summary>
    [HttpGet("session/{sessionId:guid}")]
    public async Task<ActionResult<SessionSummary>> GetSessionSummary(
        Guid sessionId,
        [FromQuery] bool regenerate = false,
        CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session is null)
        {
            return NotFound($"Session {sessionId} not found");
        }

        if (session.Extraction is null)
        {
            return BadRequest("Session has no extraction data. Please process the document first.");
        }

        // Return stored summary if available and not regenerating
        if (!regenerate && !string.IsNullOrEmpty(session.Extraction.SummaryJson))
        {
            var stored = JsonSerializer.Deserialize<SessionSummary>(session.Extraction.SummaryJson, JsonOptions);
            if (stored != null)
            {
                return Ok(stored);
            }
        }

        // Generate new summary
        var agentExtraction = new SessionSight.Agents.Models.ExtractionResult
        {
            SessionId = sessionId.ToString("D"),
            Data = session.Extraction.Data,
            OverallConfidence = session.Extraction.OverallConfidence,
            RequiresReview = session.Extraction.RequiresReview
        };

        var summary = await _summarizerAgent.SummarizeSessionAsync(agentExtraction, ct);

        // Store the new summary
        var summaryJson = JsonSerializer.Serialize(summary, JsonOptions);
        await _sessionRepository.UpdateExtractionSummaryAsync(session.Extraction.Id, summaryJson);

        return Ok(summary);
    }

    /// <summary>
    /// Gets a longitudinal summary for a patient across their sessions.
    /// </summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<ActionResult<PatientSummary>> GetPatientSummary(
        Guid patientId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId);
        if (patient is null)
        {
            return NotFound($"Patient {patientId} not found");
        }

        var summary = await _summarizerAgent.SummarizePatientAsync(patientId, startDate, endDate, ct);

        return Ok(summary);
    }

    /// <summary>
    /// Gets chart-ready risk trend points for a patient across a date range.
    /// </summary>
    [HttpGet("patient/{patientId:guid}/risk-trend")]
    public async Task<ActionResult<PatientRiskTrendDto>> GetPatientRiskTrend(
        Guid patientId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct = default)
    {
        if (startDate > endDate)
        {
            return BadRequest("startDate must be before or equal to endDate");
        }

        var patient = await _patientRepository.GetByIdAsync(patientId);
        if (patient is null)
        {
            return NotFound($"Patient {patientId} not found");
        }

        var sessions = (await _sessionRepository.GetByPatientIdInDateRangeAsync(patientId, startDate, endDate))
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.SessionNumber)
            .ToList();

        var points = sessions
            .Select(s =>
            {
                var riskLevel = s.Extraction?.Data?.RiskAssessment?.RiskLevelOverall?.Value;
                return new PatientRiskTrendPointDto(
                    s.Id,
                    s.SessionDate,
                    s.SessionNumber,
                    riskLevel?.ToString(),
                    MapRiskLevelToScore(riskLevel),
                    s.Extraction?.Data?.MoodAssessment?.SelfReportedMood?.Value,
                    s.Extraction?.RequiresReview ?? false);
            })
            .ToList();

        var latestRiskLevel = points
            .LastOrDefault(p => p.RiskScore.HasValue)?
            .RiskLevel;

        var orderedRiskScores = points
            .Where(point => point.RiskScore.HasValue)
            .Select(point => point.RiskScore!.Value)
            .ToList();

        var hasEscalation = orderedRiskScores
            .Zip(orderedRiskScores.Skip(1), (previous, current) => current > previous)
            .Any(escalated => escalated);

        var trend = new PatientRiskTrendDto(
            patientId,
            new RiskTrendPeriodDto(startDate, endDate),
            sessions.Count,
            points,
            latestRiskLevel,
            hasEscalation);

        return Ok(trend);
    }

    /// <summary>
    /// Gets a chronological patient timeline for UI rendering.
    /// </summary>
    [HttpGet("patient/{patientId:guid}/timeline")]
    public async Task<ActionResult<PatientTimelineDto>> GetPatientTimeline(
        Guid patientId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct = default)
    {
        if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
        {
            return BadRequest("startDate must be before or equal to endDate");
        }

        var patient = await _patientRepository.GetByIdAsync(patientId);
        if (patient is null)
        {
            return NotFound($"Patient {patientId} not found");
        }

        var sessions = (startDate.HasValue || endDate.HasValue
                ? await _sessionRepository.GetByPatientIdInDateRangeAsync(patientId, startDate, endDate)
                : await _sessionRepository.GetByPatientIdAsync(patientId))
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.SessionNumber)
            .ToList();

        var entries = new List<PatientTimelineEntryDto>(sessions.Count);
        DateOnly? previousSessionDate = null;
        string? previousRiskLevel = null;
        int? previousMoodScore = null;

        foreach (var session in sessions)
        {
            var extraction = session.Extraction;
            var riskLevelEnum = extraction?.Data?.RiskAssessment?.RiskLevelOverall?.Value;
            var riskLevel = riskLevelEnum?.ToString();
            var moodScore = extraction?.Data?.MoodAssessment?.SelfReportedMood?.Value;

            int? daysSincePrevious = previousSessionDate.HasValue
                ? session.SessionDate.DayNumber - previousSessionDate.Value.DayNumber
                : null;

            var riskChange = previousRiskLevel is not null && riskLevel is not null && previousRiskLevel != riskLevel
                ? $"{previousRiskLevel} -> {riskLevel}"
                : null;

            int? moodDelta = previousMoodScore.HasValue && moodScore.HasValue
                ? moodScore.Value - previousMoodScore.Value
                : null;

            var entry = new PatientTimelineEntryDto(
                session.Id,
                session.SessionDate,
                session.SessionNumber,
                session.SessionType.ToString(),
                session.Modality.ToString(),
                session.Document is not null,
                session.Document?.Status,
                session.Document?.OriginalFileName,
                session.Document?.BlobUri,
                riskLevel,
                MapRiskLevelToScore(riskLevelEnum),
                moodScore,
                extraction?.RequiresReview ?? false,
                extraction?.ReviewStatus ?? ReviewStatus.NotFlagged,
                daysSincePrevious,
                riskChange,
                moodDelta,
                MapMoodChange(moodDelta));

            entries.Add(entry);

            previousSessionDate = session.SessionDate;
            previousRiskLevel = riskLevel ?? previousRiskLevel;
            previousMoodScore = moodScore ?? previousMoodScore;
        }

        var riskScores = entries
            .Where(e => e.RiskScore.HasValue)
            .Select(e => e.RiskScore!.Value)
            .ToList();

        var hasEscalation = riskScores
            .Zip(riskScores.Skip(1), (previous, current) => current > previous)
            .Any(escalated => escalated);

        var latestRiskLevel = entries
            .LastOrDefault(e => e.RiskScore.HasValue)?
            .RiskLevel;

        return Ok(new PatientTimelineDto(
            patientId,
            startDate,
            endDate,
            entries.Count,
            entries,
            latestRiskLevel,
            hasEscalation));
    }

    /// <summary>
    /// Gets a practice-level summary aggregating metrics across all patients and sessions.
    /// </summary>
    [HttpGet("practice")]
    public async Task<ActionResult<PracticeSummary>> GetPracticeSummary(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct = default)
    {
        if (startDate > endDate)
        {
            return BadRequest("startDate must be before or equal to endDate");
        }

        var summary = await _summarizerAgent.SummarizePracticeAsync(startDate, endDate, ct);

        return Ok(summary);
    }

    private static int? MapRiskLevelToScore(RiskLevelOverall? riskLevel) => riskLevel switch
    {
        RiskLevelOverall.Low => 0,
        RiskLevelOverall.Moderate => 1,
        RiskLevelOverall.High => 2,
        RiskLevelOverall.Imminent => 3,
        _ => null
    };

    private static string? MapMoodChange(int? moodDelta) => moodDelta switch
    {
        > 0 => "improved",
        < 0 => "declined",
        0 => "unchanged",
        _ => null
    };
}
