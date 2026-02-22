using System.Text.Json;
using SessionSight.Agents.Validation;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that validates a clinical extraction against the schema.
/// Wraps <see cref="ISchemaValidator"/>.
/// Uses <see cref="LlmExtractionParser"/> to handle LLM-generated JSON
/// that cannot be directly deserialized to <c>ClinicalExtraction</c>.
/// </summary>
public class ValidateSchemaTool : IAgentTool
{
    private readonly ISchemaValidator _validator;

    public ValidateSchemaTool(ISchemaValidator validator)
    {
        _validator = validator;
    }

    public string Name => "validate_schema";

    public string Description => "Validate a clinical extraction against the schema. Returns validation errors if any fields are invalid or missing required values.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "extraction": {
                    "type": "object",
                    "description": "The clinical extraction object to validate"
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

            var result = _validator.Validate(extraction);

            return Task.FromResult(ToolResult.Ok(new ValidateSchemaOutput
            {
                IsValid = result.IsValid,
                Errors = result.Errors.Select(e => new ValidationErrorDto
                {
                    Field = e.Field,
                    Message = e.Message,
                    Severity = e.Severity.ToString()
                }).ToList()
            }));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Error($"Invalid JSON input: {ex.Message}"));
        }
    }
}

internal sealed class ValidateSchemaOutput
{
    public bool IsValid { get; set; }
    public List<ValidationErrorDto> Errors { get; set; } = [];
}

internal sealed class ValidationErrorDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
