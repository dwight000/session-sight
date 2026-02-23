using System.Text.Json;
using SessionSight.Agents.Validation;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Combined tool that validates a clinical extraction against the schema
/// AND calculates confidence scores in a single call.
/// Merges <see cref="ValidateSchemaTool"/> and <see cref="ScoreConfidenceTool"/>
/// to reduce empty-input failures from the LLM calling two tools sequentially.
/// Uses <see cref="LlmExtractionParser"/> to handle LLM-generated JSON.
/// </summary>
public class ValidateAndScoreTool : IAgentTool
{
    private readonly ISchemaValidator _validator;

    public ValidateAndScoreTool(ISchemaValidator validator)
    {
        _validator = validator;
    }

    public string Name => "validate_and_score";

    public string Description =>
        "Validate extraction against schema and calculate confidence scores. " +
        "Pass the complete extraction JSON you built. " +
        "This tool WILL FAIL if called with an empty object — " +
        "always write out the full extraction first, then call this tool.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "extraction": {
                    "type": "object",
                    "description": "The complete clinical extraction object to validate and score"
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

            // Try explicit "extraction" key first; fall back to root element
            // if the LLM passed extraction fields directly at the top level.
            JsonElement extractionElement;
            if (doc.RootElement.TryGetProperty("extraction", out var explicitExtraction) &&
                explicitExtraction.ValueKind == JsonValueKind.Object)
            {
                extractionElement = explicitExtraction;
            }
            else if (doc.RootElement.TryGetProperty("sessionInfo", out _))
            {
                // LLM passed extraction fields at root (e.g. { "sessionInfo": {...}, ... })
                extractionElement = doc.RootElement;
            }
            else
            {
                return Task.FromResult(ToolResult.Error("Missing required 'extraction' parameter"));
            }

            var extraction = LlmExtractionParser.ParseFromElement(extractionElement);
            if (extraction is null)
            {
                return Task.FromResult(ToolResult.Error("Could not parse extraction object"));
            }

            // Validation
            var validationResult = _validator.Validate(extraction);

            // Confidence scoring
            var threshold = 0.7;
            if (doc.RootElement.TryGetProperty("threshold", out var thresholdElement) &&
                thresholdElement.ValueKind == JsonValueKind.Number)
            {
                threshold = thresholdElement.GetDouble();
            }

            var overallConfidence = ConfidenceCalculator.Calculate(extraction);
            var lowConfidenceFields = ConfidenceCalculator.GetLowConfidenceFields(extraction, threshold);
            var hasLowConfidenceRiskFields = ConfidenceCalculator.HasLowConfidenceRiskFields(extraction);

            return Task.FromResult(ToolResult.Ok(new ValidateAndScoreOutput
            {
                IsValid = validationResult.IsValid,
                Errors = validationResult.Errors.Select(e => new ValidationErrorDto
                {
                    Field = e.Field,
                    Message = e.Message,
                    Severity = e.Severity.ToString()
                }).ToList(),
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

internal sealed class ValidateAndScoreOutput
{
    public bool IsValid { get; set; }
    public List<ValidationErrorDto> Errors { get; set; } = [];
    public double OverallConfidence { get; set; }
    public List<string> LowConfidenceFields { get; set; } = [];
    public bool HasLowConfidenceRiskFields { get; set; }
    public double Threshold { get; set; }
}

internal sealed class ValidationErrorDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
