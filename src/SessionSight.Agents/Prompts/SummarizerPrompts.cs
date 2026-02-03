namespace SessionSight.Agents.Prompts;

/// <summary>
/// Prompts for the Summarizer Agent.
/// Used to generate session, patient, and practice-level summaries.
/// </summary>
public static class SummarizerPrompts
{
    /// <summary>
    /// System prompt for summarization tasks.
    /// </summary>
    public const string SystemPrompt = """
        You are a clinical documentation specialist creating concise, actionable summaries
        of therapy session data. Your summaries should:

        1. Be CONCISE - therapists are busy, get to the point
        2. Be CLINICAL - use appropriate professional language
        3. Be ACTIONABLE - highlight what matters for treatment continuity
        4. Be ACCURATE - only summarize what is present in the data
        5. Preserve CONFIDENTIALITY - summaries may be stored, avoid unnecessary identifying details

        Never speculate or add information not present in the source data.
        Focus on clinical relevance and treatment continuity.
        """;

    /// <summary>
    /// Gets the prompt for generating a session summary from extraction data.
    /// </summary>
    public static string GetSessionSummaryPrompt(string extractionJson) => $$"""
        Create a concise summary of this therapy session from the extracted clinical data.

        Extracted Data:
        ---
        {{extractionJson}}
        ---

        Generate a summary with these fields:

        1. oneLiner: A 2-3 sentence summary capturing the essence of the session
           (presenting concerns, key interventions, immediate outcomes)

        2. keyPoints: Bullet points of the most important clinical observations
           (mood state, cognitive patterns, behavioral changes, progress markers)

        3. interventionsUsed: List of therapeutic interventions/techniques used
           (e.g., CBT, mindfulness, exposure, etc.)

        4. nextSessionFocus: Brief note on recommended focus for next session

        5. riskFlags (if applicable):
           - riskLevel: The overall risk level (Low, Moderate, High, Imminent)
           - flags: Any specific risk indicators to note
           - requiresReview: Whether clinical review is needed

        Return JSON in this format:
        {
          "oneLiner": "...",
          "keyPoints": "...",
          "interventionsUsed": ["..."],
          "nextSessionFocus": "...",
          "riskFlags": {
            "riskLevel": "Low",
            "flags": [],
            "requiresReview": false
          }
        }

        If risk assessment shows no concerns, set riskFlags to null.
        Keep the summary focused and clinically relevant.
        """;

    /// <summary>
    /// Gets the prompt for generating a patient summary from multiple sessions.
    /// </summary>
    public static string GetPatientSummaryPrompt(string sessionsJson, int sessionCount) => $$"""
        Create a longitudinal summary for this patient based on {{sessionCount}} therapy sessions.

        Session Data (ordered chronologically):
        ---
        {{sessionsJson}}
        ---

        Generate a comprehensive patient summary with these fields:

        1. progressNarrative: A paragraph describing the patient's overall progress,
           trajectory, and treatment response across the period.

        2. moodTrend: Overall mood trend observed (Improving, Stable, Declining, Variable, InsufficientData)

        3. recurringThemes: Key themes that appear across multiple sessions
           (relationship issues, work stress, trauma processing, etc.)

        4. goalProgress: Treatment goals and their current status
           (format as array of {"goal": "...", "status": "..."})

        5. effectiveInterventions: Interventions that have shown positive results

        6. recommendedFocus: Synthesis of what should be prioritized going forward

        7. riskTrendSummary: Brief description of risk patterns across the period
           (e.g., "No risk concerns", "Elevated risk in early sessions, now stable", etc.)

        Return JSON in this format:
        {
          "progressNarrative": "...",
          "moodTrend": "Improving",
          "recurringThemes": ["..."],
          "goalProgress": [{"goal": "...", "status": "..."}],
          "effectiveInterventions": ["..."],
          "recommendedFocus": "...",
          "riskTrendSummary": "..."
        }

        Base your summary ONLY on the data provided. Do not speculate about
        information not present in the session records.
        """;

    /// <summary>
    /// Gets the prompt for practice-level summarization (primarily metrics, minimal LLM needed).
    /// </summary>
    public static string GetPracticeSummaryPrompt(string metricsJson) => $$"""
        Review these practice metrics and provide brief narrative observations.

        Practice Metrics:
        ---
        {{metricsJson}}
        ---

        Return a JSON object with any notable observations about the practice data:
        {
          "observations": "Brief 2-3 sentence summary of notable patterns or concerns"
        }

        Focus only on clinically relevant patterns. If metrics appear normal, state that briefly.
        """;
}
