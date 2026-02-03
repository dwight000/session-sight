using SessionSight.Agents.Models;
using AgentExtractionResult = SessionSight.Agents.Models.ExtractionResult;

namespace SessionSight.Agents.Agents;

/// <summary>
/// Interface for the Summarizer Agent.
/// Generates summaries at session, patient, and practice levels.
/// </summary>
public interface ISummarizerAgent : ISessionSightAgent
{
    /// <summary>
    /// Generates a summary for a single therapy session.
    /// </summary>
    /// <param name="extraction">The extraction result to summarize.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Session summary with key points and risk flags.</returns>
    Task<SessionSummary> SummarizeSessionAsync(
        AgentExtractionResult extraction,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a longitudinal summary for a patient across sessions.
    /// </summary>
    /// <param name="patientId">The patient ID to summarize.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Patient summary with progress narrative and trends.</returns>
    Task<PatientSummary> SummarizePatientAsync(
        Guid patientId,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a practice-level summary aggregating metrics across sessions.
    /// </summary>
    /// <param name="startDate">Start date for the period.</param>
    /// <param name="endDate">End date for the period.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Practice summary with metrics and flagged patients.</returns>
    Task<PracticeSummary> SummarizePracticeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}
