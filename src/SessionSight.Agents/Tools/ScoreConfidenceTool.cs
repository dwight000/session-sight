using System.Text.Json;
using SessionSight.Agents.Validation;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that calculates confidence scores for a clinical extraction.
/// Wraps <see cref="ConfidenceCalculator"/>.
/// Uses <see cref="LlmExtractionParser"/> to handle LLM-generated JSON
/// that cannot be directly deserialized to <c>ClinicalExtraction</c>.
/// </summary>
public class ScoreConfidenceTool : IAgentTool
{
    public string Name => "score_confidence";

    public string Description => "Calculate confidence scores for a clinical extraction. Returns overall confidence score and list of any low-confidence fields.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "extraction": {
                    "type": "object",
                    "description": "The clinical extraction object to score"
                },
                "threshold": {
                    "type": "number",
                    "description": "Confidence threshold for flagging low-confidence fields (default 0.7)"
                }
            },
            "required": ["extraction"]
        }
        """);

    public Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input.ToStream());
            if (!doc.RootElement.TryGetProperty("extraction", out var extractionElement) ||
                extractionElement.ValueKind != JsonValueKind.Object)
            {
                return Task.FromResult(ToolResult.Error("Missing required 'extraction' parameter"));
            }

            var extraction = LlmExtractionParser.ParseFromElement(extractionElement);
            if (extraction is null)
            {
                return Task.FromResult(ToolResult.Error("Could not parse extraction object"));
            }

            var threshold = 0.7;
            if (doc.RootElement.TryGetProperty("threshold", out var thresholdElement) &&
                thresholdElement.ValueKind == JsonValueKind.Number)
            {
                threshold = thresholdElement.GetDouble();
            }

            var overallConfidence = ConfidenceCalculator.Calculate(extraction);
            var lowConfidenceFields = ConfidenceCalculator.GetLowConfidenceFields(extraction, threshold);
            var hasLowConfidenceRiskFields = ConfidenceCalculator.HasLowConfidenceRiskFields(extraction);

            return Task.FromResult(ToolResult.Ok(new ScoreConfidenceOutput
            {
                OverallConfidence = overallConfidence,
                LowConfidenceFields = lowConfidenceFields,
                HasLowConfidenceRiskFields = hasLowConfidenceRiskFields,
                Threshold = threshold
            }));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Error($"Invalid JSON input: {ex.Message}"));
        }
    }
}

internal sealed class ScoreConfidenceOutput
{
    public double OverallConfidence { get; set; }
    public List<string> LowConfidenceFields { get; set; } = [];
    public bool HasLowConfidenceRiskFields { get; set; }
    public double Threshold { get; set; }
}
