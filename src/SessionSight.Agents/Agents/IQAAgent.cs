using SessionSight.Agents.Models;

namespace SessionSight.Agents.Agents;

/// <summary>
/// Interface for the Q&amp;A Agent.
/// Answers clinical questions about patient sessions using RAG.
/// </summary>
public interface IQAAgent : ISessionSightAgent
{
    /// <summary>
    /// Answers a clinical question about a specific patient using RAG search.
    /// </summary>
    /// <param name="question">The clinical question to answer.</param>
    /// <param name="patientId">The patient ID to scope the search to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>QA response with answer, sources, and confidence.</returns>
    Task<QAResponse> AnswerAsync(string question, Guid patientId, CancellationToken ct = default);
}
