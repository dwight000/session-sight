using System.Text.Json;
using SessionSight.Agents.Validation;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that calculates confidence scores for a clinical extraction.
/// Wraps <see cref="ConfidenceCalculator"/>.
/// </summary>
public class ScoreConfidenceTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            var request = JsonSerializer.Deserialize<ScoreConfidenceInput>(input.ToStream(), JsonOptions);

            if (request?.Extraction is null)
            {
                return Task.FromResult(ToolResult.Error("Missing required 'extraction' parameter"));
            }

            var threshold = request.Threshold ?? 0.7;
            var overallConfidence = ConfidenceCalculator.Calculate(request.Extraction);
            var lowConfidenceFields = ConfidenceCalculator.GetLowConfidenceFields(request.Extraction, threshold);
            var hasLowConfidenceRiskFields = ConfidenceCalculator.HasLowConfidenceRiskFields(request.Extraction);

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

internal sealed class ScoreConfidenceInput
{
    public ClinicalExtraction? Extraction { get; set; }
    public double? Threshold { get; set; }
}

internal sealed class ScoreConfidenceOutput
{
    public double OverallConfidence { get; set; }
    public List<string> LowConfidenceFields { get; set; } = [];
    public bool HasLowConfidenceRiskFields { get; set; }
    public double Threshold { get; set; }
}
