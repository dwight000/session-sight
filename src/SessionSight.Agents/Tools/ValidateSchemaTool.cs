using System.Text.Json;
using SessionSight.Agents.Validation;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that validates a clinical extraction against the schema.
/// Wraps <see cref="ISchemaValidator"/>.
/// </summary>
public class ValidateSchemaTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            var wrapper = JsonSerializer.Deserialize<ValidateSchemaInput>(input.ToStream(), JsonOptions);

            if (wrapper?.Extraction is null)
            {
                return Task.FromResult(ToolResult.Error("Missing required 'extraction' parameter"));
            }

            var result = _validator.Validate(wrapper.Extraction);

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

internal class ValidateSchemaInput
{
    public ClinicalExtraction? Extraction { get; set; }
}

internal class ValidateSchemaOutput
{
    public bool IsValid { get; set; }
    public List<ValidationErrorDto> Errors { get; set; } = [];
}

internal class ValidationErrorDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
