using System.Text.Json.Serialization;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Models;

/// <summary>
/// Parsed response for risk re-extraction, including optional structured criteria feedback.
/// </summary>
public sealed class RiskReExtractionResponse
{
    public RiskAssessmentExtracted Risk { get; set; } = new();

    /// <summary>
    /// Optional criteria used by the model for each risk field.
    /// Keys are snake_case field names (e.g., "self_harm", "homicidal_ideation").
    /// </summary>
    public Dictionary<string, List<string>> CriteriaUsed { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Freeform per-field rationale from the model for debugging/tuning.
    /// Keys are snake_case field names (e.g., "self_harm", "homicidal_ideation").
    /// </summary>
    public Dictionary<string, string> ReasoningUsed { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Number of model attempts used to obtain a criteria-valid response.
    /// </summary>
    public int CriteriaValidationAttemptsUsed { get; set; } = 1;
}

internal sealed class RiskCriteriaEnvelope
{
    [JsonPropertyName("criteria_used")]
    public Dictionary<string, List<string>>? CriteriaUsed { get; set; }
}
