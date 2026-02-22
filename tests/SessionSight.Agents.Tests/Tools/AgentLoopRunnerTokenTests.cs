using FluentAssertions;
using SessionSight.Agents.Tools;

namespace SessionSight.Agents.Tests.Tools;

public class AgentLoopRunnerTokenTests
{
    [Fact]
    public void Complete_WithTokens_PopulatesAllTokenFields()
    {
        var result = AgentLoopResult.Complete("done",
            inputTokens: 100, outputTokens: 50, totalTokens: 150);

        result.InputTokens.Should().Be(100);
        result.OutputTokens.Should().Be(50);
        result.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void Partial_WithTokens_PopulatesAllTokenFields()
    {
        var result = AgentLoopResult.Partial("timeout",
            inputTokens: 200, outputTokens: 80, totalTokens: 280);

        result.InputTokens.Should().Be(200);
        result.OutputTokens.Should().Be(80);
        result.TotalTokens.Should().Be(280);
    }

    [Fact]
    public void Complete_DefaultTokens_AreZero()
    {
        var result = AgentLoopResult.Complete("done");

        result.InputTokens.Should().Be(0);
        result.OutputTokens.Should().Be(0);
        result.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void ToolCallEntry_WithLoopRoundAndDuration_PreservesValues()
    {
        var entry = new ToolCallEntry("TestTool", true, LoopRound: 2, DurationMs: 150);

        entry.ToolName.Should().Be("TestTool");
        entry.Succeeded.Should().BeTrue();
        entry.LoopRound.Should().Be(2);
        entry.DurationMs.Should().Be(150);
    }

    [Fact]
    public void ToolCallEntry_DefaultParams_AreZero()
    {
        var entry = new ToolCallEntry("TestTool", false);

        entry.LoopRound.Should().Be(0);
        entry.DurationMs.Should().Be(0);
    }

    [Fact]
    public void Complete_WithToolCallTrace_PreservesTrace()
    {
        var trace = new List<ToolCallEntry>
        {
            new("Tool1", true, 0, 10),
            new("Tool2", true, 1, 20),
            new("Tool1", false, 2, 5)
        };

        var result = AgentLoopResult.Complete("done", toolCallCount: 3,
            toolCallTrace: trace, inputTokens: 500, outputTokens: 200, totalTokens: 700);

        result.ToolCallTrace.Should().HaveCount(3);
        result.ToolCallTrace[1].LoopRound.Should().Be(1);
        result.ToolCallTrace[2].Succeeded.Should().BeFalse();
    }
}
