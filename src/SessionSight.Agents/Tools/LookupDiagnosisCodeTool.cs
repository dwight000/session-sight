using System.Text.Json;
using System.Text.RegularExpressions;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that looks up diagnosis codes (ICD-10/DSM-5).
/// Currently a stub - returns basic validation.
/// </summary>
public class LookupDiagnosisCodeTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Common ICD-10 mental health codes for validation
    private static readonly Dictionary<string, string> CommonCodes = new()
    {
        ["F32.0"] = "Major depressive disorder, single episode, mild",
        ["F32.1"] = "Major depressive disorder, single episode, moderate",
        ["F32.2"] = "Major depressive disorder, single episode, severe without psychotic features",
        ["F33.0"] = "Major depressive disorder, recurrent, mild",
        ["F33.1"] = "Major depressive disorder, recurrent, moderate",
        ["F41.0"] = "Panic disorder",
        ["F41.1"] = "Generalized anxiety disorder",
        ["F43.10"] = "Post-traumatic stress disorder, unspecified",
        ["F43.11"] = "Post-traumatic stress disorder, acute",
        ["F43.12"] = "Post-traumatic stress disorder, chronic",
        ["F90.0"] = "Attention-deficit hyperactivity disorder, predominantly inattentive type",
        ["F90.1"] = "Attention-deficit hyperactivity disorder, predominantly hyperactive type",
        ["F90.2"] = "Attention-deficit hyperactivity disorder, combined type"
    };

    public string Name => "lookup_diagnosis_code";

    public string Description => "Look up an ICD-10 or DSM-5 diagnosis code. Returns the description if valid, or indicates if the code is unknown.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "The diagnosis code to look up (e.g., F32.1, F41.1)"
                }
            },
            "required": ["code"]
        }
        """);

    public Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = JsonSerializer.Deserialize<LookupDiagnosisCodeInput>(input.ToStream(), JsonOptions);

            if (string.IsNullOrEmpty(request?.Code))
            {
                return Task.FromResult(ToolResult.Error("Missing required 'code' parameter"));
            }

            var normalizedCode = request.Code.Trim().ToUpperInvariant();

            if (CommonCodes.TryGetValue(normalizedCode, out var description))
            {
                return Task.FromResult(ToolResult.Ok(new LookupDiagnosisCodeOutput
                {
                    Code = normalizedCode,
                    Description = description,
                    IsValid = true,
                    CodeSystem = normalizedCode.StartsWith('F') ? "ICD-10" : "Unknown"
                }));
            }

            // Basic format validation for ICD-10 mental health codes (F00-F99)
            var isValidFormat = Regex.IsMatch(normalizedCode, @"^F\d{2}(\.\d{1,2})?$");

            return Task.FromResult(ToolResult.Ok(new LookupDiagnosisCodeOutput
            {
                Code = normalizedCode,
                Description = isValidFormat ? "Code not in local database" : "Invalid code format",
                IsValid = isValidFormat,
                CodeSystem = isValidFormat ? "ICD-10" : "Unknown"
            }));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Error($"Invalid JSON input: {ex.Message}"));
        }
    }
}

internal sealed class LookupDiagnosisCodeInput
{
    public string? Code { get; set; }
}

internal sealed class LookupDiagnosisCodeOutput
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string CodeSystem { get; set; } = string.Empty;
}
