using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionSight.Agents.Helpers;

/// <summary>
/// Shared JSON serializer options for agent deserialization.
/// </summary>
public static class SharedJsonOptions
{
    /// <summary>
    /// Default options for agent response parsing: case-insensitive, camelCase naming.
    /// </summary>
    public static readonly JsonSerializerOptions AgentDefault = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Options that also allow reading numbers from JSON strings (for IntakeAgent).
    /// </summary>
    public static readonly JsonSerializerOptions AgentWithNumberHandling = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
