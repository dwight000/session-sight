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
}
