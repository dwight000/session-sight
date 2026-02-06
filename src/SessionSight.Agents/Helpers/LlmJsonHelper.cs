using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SessionSight.Agents.Helpers;

/// <summary>
/// Shared helpers for parsing LLM JSON responses.
/// Handles common issues: code fences, prose wrapping, string-typed numbers.
/// </summary>
public static partial class LlmJsonHelper
{
    /// <summary>
    /// Extracts JSON from LLM responses that may include code fences or prose.
    /// Handles: fences at start, fences mid-content, outermost braces.
    /// </summary>
    public static string ExtractJson(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIndex > 7)
            {
                return trimmed[7..endIndex].Trim();
            }
        }

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var startIndex = trimmed.IndexOf('\n') + 1;
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                return trimmed[startIndex..endIndex].Trim();
            }
        }

        // Try to find JSON code fence anywhere in the content (prose before/after)
        var fenceMatch = JsonFenceRegex().Match(trimmed);
        if (fenceMatch.Success)
        {
            return fenceMatch.Groups[1].Value.Trim();
        }

        // Last resort: find outermost { ... } braces
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }

    /// <summary>
    /// Parses a confidence value that may be a number or string.
    /// Returns null if the element cannot be parsed as a double.
    /// </summary>
    public static double? TryParseConfidence(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDouble(out var d) ? d : null,
            JsonValueKind.String when double.TryParse(element.GetString(),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }

    /// <summary>
    /// Parses an int from a JsonElement that may be a number, float, or string.
    /// </summary>
    public static int? TryParseInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out var i)) return i;
            if (element.TryGetDouble(out var d)) return (int)d;
            return null;
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Parses a double from a JsonElement that may be a number or string.
    /// </summary>
    public static double? TryParseDouble(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetDouble(out var d) ? d : null;

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?([\s\S]*?)```", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JsonFenceRegex();
}
