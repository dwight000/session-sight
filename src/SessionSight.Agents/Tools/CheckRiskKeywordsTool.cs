using System.Text.Json;
using SessionSight.Agents.Validation;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Tool that scans text for suicide, self-harm, and homicidal keywords.
/// Wraps <see cref="DangerKeywordChecker"/>.
/// </summary>
public class CheckRiskKeywordsTool : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "check_risk_keywords";

    public string Description => "Scan text for suicide, self-harm, and homicidal keywords. Returns lists of matched keywords by category.";

    public BinaryData InputSchema { get; } = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "text": {
                    "type": "string",
                    "description": "The therapy note text to scan for danger keywords"
                }
            },
            "required": ["text"]
        }
        """);

    public Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default)
    {
        try
        {
            var request = JsonSerializer.Deserialize<CheckRiskKeywordsInput>(input.ToStream(), JsonOptions);

            if (request?.Text is null)
            {
                return Task.FromResult(ToolResult.Error("Missing required 'text' parameter"));
            }

            var result = DangerKeywordChecker.Check(request.Text);

            return Task.FromResult(ToolResult.Ok(new CheckRiskKeywordsOutput
            {
                SuicidalMatches = result.SuicidalMatches,
                SelfHarmMatches = result.SelfHarmMatches,
                HomicidalMatches = result.HomicidalMatches,
                HasAnyMatches = result.HasAnyMatches
            }));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Error($"Invalid JSON input: {ex.Message}"));
        }
    }
}

internal sealed class CheckRiskKeywordsInput
{
    public string? Text { get; set; }
}

internal sealed class CheckRiskKeywordsOutput
{
    public List<string> SuicidalMatches { get; set; } = [];
    public List<string> SelfHarmMatches { get; set; } = [];
    public List<string> HomicidalMatches { get; set; } = [];
    public bool HasAnyMatches { get; set; }
}
