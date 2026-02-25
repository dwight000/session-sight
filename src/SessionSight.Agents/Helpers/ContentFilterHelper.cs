using OpenAI.Chat;

namespace SessionSight.Agents.Helpers;

/// <summary>
/// Shared helper for detecting content filter blocks from Azure OpenAI responses.
/// </summary>
public static class ContentFilterHelper
{
    /// <summary>
    /// Checks whether a chat completion was blocked by the content filter.
    /// Only checks FinishReason — do NOT use Content.Count == 0 as a proxy,
    /// because tool call responses legitimately have empty Content.
    /// </summary>
    public static bool IsContentFilterBlocked(ChatCompletion completion) =>
        completion.FinishReason == ChatFinishReason.ContentFilter;
}
