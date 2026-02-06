namespace SessionSight.Agents.Models;

/// <summary>
/// Request body for the Q&amp;A endpoint.
/// </summary>
public class QARequest
{
    /// <summary>
    /// The clinical question to answer.
    /// </summary>
    public string Question { get; set; } = string.Empty;
}

/// <summary>
/// Response from the Q&amp;A agent with answer and source citations.
/// </summary>
public class QAResponse
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<SourceCitation> Sources { get; set; } = new();
    public double Confidence { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public string? Warning { get; set; }
    public int ToolCallCount { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// A source citation referencing a specific therapy session.
/// </summary>
public class SourceCitation
{
    public string SessionId { get; set; } = string.Empty;
    public DateTimeOffset SessionDate { get; set; }
    public string? SessionType { get; set; }
    public string? Summary { get; set; }
    public double RelevanceScore { get; set; }
}
