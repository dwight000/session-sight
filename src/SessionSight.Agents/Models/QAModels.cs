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
    public QADiagnostics? Diagnostics { get; set; }
}

/// <summary>
/// Debug diagnostics for a Q&amp;A response — always populated, no config flag needed.
/// </summary>
public class QADiagnostics
{
    public bool IsComplex { get; set; }
    public string? Reasoning { get; set; }
    public int SearchResultCount { get; set; }
    public List<QAToolCallEntry> ToolCalls { get; set; } = [];
}

/// <summary>
/// A single tool invocation recorded during a Q&amp;A agentic loop.
/// </summary>
public class QAToolCallEntry
{
    public string ToolName { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
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
