using Microsoft.Extensions.AI;

namespace SessionSight.Agents.Helpers;

/// <summary>
/// Shared helper for detecting content filter blocks from Azure OpenAI responses.
/// </summary>
public static class ContentFilterHelper
{
    /// <summary>
    /// Checks whether a chat response was blocked by the content filter.
    /// Only checks FinishReason — do NOT use empty Text as a proxy,
    /// because tool call responses legitimately have empty Text.
    /// </summary>
    public static bool IsContentFilterBlocked(ChatResponse response) =>
        response.FinishReason == ChatFinishReason.ContentFilter;
}
