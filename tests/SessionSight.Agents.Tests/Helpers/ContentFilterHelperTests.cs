using FluentAssertions;
using Microsoft.Extensions.AI;
using SessionSight.Agents.Helpers;

namespace SessionSight.Agents.Tests.Helpers;

public class ContentFilterHelperTests
{
    [Fact]
    public void IsContentFilterBlocked_ContentFilterFinishReason_ReturnsTrue()
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "blocked content"))
        {
            FinishReason = ChatFinishReason.ContentFilter
        };

        ContentFilterHelper.IsContentFilterBlocked(response).Should().BeTrue();
    }

    [Fact]
    public void IsContentFilterBlocked_EmptyContent_WithStopReason_ReturnsFalse()
    {
        // Empty content with Stop finish reason is NOT content filter —
        // tool call responses legitimately have empty Text.
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, (string?)null))
        {
            FinishReason = ChatFinishReason.Stop
        };

        ContentFilterHelper.IsContentFilterBlocked(response).Should().BeFalse();
    }

    [Fact]
    public void IsContentFilterBlocked_EmptyContent_WithToolCallsReason_ReturnsFalse()
    {
        // Tool call responses have empty Content but are NOT content filter blocked.
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, (string?)null))
        {
            FinishReason = ChatFinishReason.ToolCalls
        };

        ContentFilterHelper.IsContentFilterBlocked(response).Should().BeFalse();
    }

    [Fact]
    public void IsContentFilterBlocked_NormalCompletion_ReturnsFalse()
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Normal response text"))
        {
            FinishReason = ChatFinishReason.Stop
        };

        ContentFilterHelper.IsContentFilterBlocked(response).Should().BeFalse();
    }
}
