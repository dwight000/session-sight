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

    [Fact]
    public void ToolCallEntry_WithInputOutputJson_PreservesValues()
    {
        var entry = new ToolCallEntry("ValidateSchema", true, 0, 50,
            """{"schema":"clinical"}""", """{"valid":true}""");

        entry.InputJson.Should().Contain("clinical");
        entry.OutputJson.Should().Contain("valid");
    }

    [Fact]
    public void ToolCallEntry_DefaultInputOutputJson_AreNull()
    {
        var entry = new ToolCallEntry("TestTool", true);

        entry.InputJson.Should().BeNull();
        entry.OutputJson.Should().BeNull();
    }

    [Fact]
    public void LlmCallTrace_PreservesAllFields()
    {
        var trace = new LlmCallTrace(
            "Extract metadata from document",
            """{"isValid":true}""",
            "gpt-4.1-nano",
            LoopRound: 0,
            InputTokens: 200,
            OutputTokens: 100,
            TotalTokens: 300,
            DurationMs: 450);

        trace.PromptText.Should().Contain("Extract metadata");
        trace.ResponseText.Should().Contain("isValid");
        trace.ModelUsed.Should().Be("gpt-4.1-nano");
        trace.LoopRound.Should().Be(0);
        trace.InputTokens.Should().Be(200);
        trace.OutputTokens.Should().Be(100);
        trace.TotalTokens.Should().Be(300);
        trace.DurationMs.Should().Be(450);
    }

    [Fact]
    public void Complete_WithLlmTraces_PreservesTraces()
    {
        var llmTraces = new List<LlmCallTrace>
        {
            new("prompt1", "response1", "gpt-4.1-mini", 0, 100, 50, 150, 200),
            new("prompt2", "response2", "gpt-4.1-mini", 1, 80, 40, 120, 180)
        };

        var result = AgentLoopResult.Complete("done",
            inputTokens: 180, outputTokens: 90, totalTokens: 270,
            llmTraces: llmTraces);

        result.LlmTraces.Should().HaveCount(2);
        result.LlmTraces[0].LoopRound.Should().Be(0);
        result.LlmTraces[1].LoopRound.Should().Be(1);
    }

    [Fact]
    public void Complete_DefaultLlmTraces_IsEmpty()
    {
        var result = AgentLoopResult.Complete("done");

        result.LlmTraces.Should().BeEmpty();
    }

    [Fact]
    public void Partial_WithLlmTraces_PreservesTraces()
    {
        var llmTraces = new List<LlmCallTrace>
        {
            new("prompt", "response", "gpt-4.1-mini", 0, 100, 50, 150, 200)
        };

        var result = AgentLoopResult.Partial("timeout",
            inputTokens: 100, outputTokens: 50, totalTokens: 150,
            llmTraces: llmTraces);

        result.LlmTraces.Should().HaveCount(1);
    }
}
