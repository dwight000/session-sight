using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenAI.Chat;
using SessionSight.Agents.Tools;

namespace SessionSight.Agents.Tests.Tools;

public class AgentLoopRunnerTests
{
    [Fact]
    public void LoopTimeout_IsFiveMinutes()
    {
        AgentLoopRunner.LoopTimeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void MaxToolCalls_IsFifteen()
    {
        AgentLoopRunner.MaxToolCalls.Should().Be(15);
    }
}
