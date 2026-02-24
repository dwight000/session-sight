using OpenAI.Chat;

namespace SessionSight.Agents.Helpers;

/// <summary>
/// Shared helper for detecting content filter blocks from Azure OpenAI responses.
/// </summary>
public static class ContentFilterHelper
{
    /// <summary>
    /// Checks whether a chat completion was blocked by the content filter.
    /// A response is considered blocked if the finish reason is ContentFilter
    /// or the response has no content.
    /// </summary>
    public static bool IsContentFilterBlocked(ChatCompletion completion) =>
        completion.FinishReason == ChatFinishReason.ContentFilter
        || completion.Content.Count == 0;
}
