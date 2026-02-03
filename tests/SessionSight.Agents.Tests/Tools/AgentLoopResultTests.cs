using FluentAssertions;
using SessionSight.Agents.Tools;

namespace SessionSight.Agents.Tests.Tools;

public class AgentLoopResultTests
{
    [Fact]
    public void Complete_WithContent_SetsIsCompleteTrue()
    {
        var result = AgentLoopResult.Complete("extraction complete");

        result.IsComplete.Should().BeTrue();
        result.IsPartial.Should().BeFalse();
        result.Content.Should().Be("extraction complete");
        result.PartialReason.Should().BeNull();
    }

    [Fact]
    public void Complete_WithContentAndToolCallCount_SetsBothValues()
    {
        var result = AgentLoopResult.Complete("done", toolCallCount: 5);

        result.IsComplete.Should().BeTrue();
        result.Content.Should().Be("done");
        result.ToolCallCount.Should().Be(5);
    }

    [Fact]
    public void Complete_WithZeroToolCalls_DefaultsToZero()
    {
        var result = AgentLoopResult.Complete("content");

        result.ToolCallCount.Should().Be(0);
    }

    [Fact]
    public void Partial_WithReason_SetsIsCompleteFalse()
    {
        var result = AgentLoopResult.Partial("max iterations reached");

        result.IsComplete.Should().BeFalse();
        result.IsPartial.Should().BeTrue();
        result.PartialReason.Should().Be("max iterations reached");
        result.Content.Should().BeNull();
    }

    [Fact]
    public void Partial_WithReasonAndToolCallCount_SetsBothValues()
    {
        var result = AgentLoopResult.Partial("timeout", toolCallCount: 3);

        result.IsComplete.Should().BeFalse();
        result.PartialReason.Should().Be("timeout");
        result.ToolCallCount.Should().Be(3);
    }

    [Fact]
    public void Partial_WithZeroToolCalls_DefaultsToZero()
    {
        var result = AgentLoopResult.Partial("stopped");

        result.ToolCallCount.Should().Be(0);
    }

    [Fact]
    public void IsPartial_IsInverseOfIsComplete()
    {
        var complete = AgentLoopResult.Complete("done");
        var partial = AgentLoopResult.Partial("stopped");

        complete.IsPartial.Should().BeFalse();
        partial.IsPartial.Should().BeTrue();
    }
}
