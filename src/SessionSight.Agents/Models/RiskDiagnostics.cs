namespace SessionSight.Agents.Models;

/// <summary>
/// Structured diagnostics for risk-field resolution across assessment stages.
/// </summary>
public sealed class RiskDiagnostics
{
    /// <summary>
    /// Per-field decision details across original, re-extracted, and final values.
    /// </summary>
    public List<RiskFieldDiagnostic> Decisions { get; set; } = new();

    /// <summary>
    /// Whether homicidal guardrail logic changed the final value.
    /// </summary>
    public bool HomicidalGuardrailApplied { get; set; }

    /// <summary>
    /// Optional explanation for guardrail application.
    /// </summary>
    public string? HomicidalGuardrailReason { get; set; }

    /// <summary>
    /// Homicidal keyword matches seen by safety-net checker.
    /// </summary>
    public List<string> HomicidalKeywordMatches { get; set; } = new();

    /// <summary>
    /// Whether self-harm guardrail logic changed the final value.
    /// </summary>
    public bool SelfHarmGuardrailApplied { get; set; }

    /// <summary>
    /// Optional explanation for self-harm guardrail application.
    /// </summary>
    public string? SelfHarmGuardrailReason { get; set; }

    /// <summary>
    /// Number of model attempts used by criteria validation retry loop.
    /// </summary>
    public int CriteriaValidationAttemptsUsed { get; set; } = 1;

}

/// <summary>
/// Structured diagnostic details for a single risk field.
/// </summary>
public sealed class RiskFieldDiagnostic
{
    public string Field { get; set; } = string.Empty;

    public string OriginalValue { get; set; } = string.Empty;

    public string ReExtractedValue { get; set; } = string.Empty;

    public string FinalValue { get; set; } = string.Empty;

    public string RuleApplied { get; set; } = string.Empty;

    public string? OriginalSource { get; set; }

    public string? ReExtractedSource { get; set; }

    public string? FinalSource { get; set; }

    public List<string> CriteriaUsed { get; set; } = new();

    public string ReasoningUsed { get; set; } = string.Empty;
}
