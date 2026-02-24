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
    public void IsContentFilterBlocked_EmptyContent_ReturnsTrue()
    {
        var completion = OpenAIChatModelFactory.ChatCompletion(
            finishReason: ChatFinishReason.Stop,
            content: new ChatMessageContent());

        ContentFilterHelper.IsContentFilterBlocked(completion).Should().BeTrue();
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
