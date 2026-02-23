using SessionSight.Agents.Models;

namespace SessionSight.Agents.Orchestration;

/// <summary>
/// Contract for the extraction orchestrator that coordinates the full pipeline.
/// </summary>
public interface IExtractionOrchestrator
{
    /// <summary>
    /// Processes a session's document through the full extraction pipeline:
    /// Document Intelligence -> Intake Agent -> Clinical Extractor -> Risk Assessor -> Database
    /// </summary>
    /// <param name="sessionId">The session ID with an uploaded document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The orchestration result with extraction ID and status.</returns>
    Task<OrchestrationResult> ProcessSessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Generates (or regenerates) a session summary from the existing extraction data.
    /// Calls the summarizer agent and persists the resulting JSON.
    /// </summary>
    /// <param name="sessionId">The session whose extraction to summarize.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated session summary.</returns>
    Task<SessionSummary> GenerateSessionSummaryAsync(Guid sessionId, CancellationToken ct = default);
}
