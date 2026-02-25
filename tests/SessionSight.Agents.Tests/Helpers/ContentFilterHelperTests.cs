using FluentAssertions;
using OpenAI.Chat;
using SessionSight.Agents.Helpers;

namespace SessionSight.Agents.Tests.Helpers;

public class ContentFilterHelperTests
{
    [Fact]
    public void IsContentFilterBlocked_ContentFilterFinishReason_ReturnsTrue()
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            finishReason: ChatFinishReason.ContentFilter,
            content: new ChatMessageContent("blocked content"));

        ContentFilterHelper.IsContentFilterBlocked(completion).Should().BeTrue();
    }

    [Fact]
    public void IsContentFilterBlocked_EmptyContent_WithStopReason_ReturnsFalse()
    {
        // Empty content with Stop finish reason is NOT content filter —
        // tool call responses legitimately have empty Content.
        var completion = OpenAIChatModelFactory.ChatCompletion(
            finishReason: ChatFinishReason.Stop,
            content: new ChatMessageContent());

        ContentFilterHelper.IsContentFilterBlocked(completion).Should().BeFalse();
    }

    [Fact]
    public void IsContentFilterBlocked_EmptyContent_WithToolCallsReason_ReturnsFalse()
    {
        // Tool call responses have empty Content but are NOT content filter blocked.
        var completion = OpenAIChatModelFactory.ChatCompletion(
            finishReason: ChatFinishReason.ToolCalls,
            content: new ChatMessageContent());

        ContentFilterHelper.IsContentFilterBlocked(completion).Should().BeFalse();
    }

    [Fact]
    public void IsContentFilterBlocked_NormalCompletion_ReturnsFalse()
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            finishReason: ChatFinishReason.Stop,
            content: new ChatMessageContent("Normal response text"));

        ContentFilterHelper.IsContentFilterBlocked(completion).Should().BeFalse();
    }
}
