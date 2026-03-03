using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;
using SessionSight.Agents.Tools;

namespace SessionSight.Agents.Tests.Tools;

public class AgentToolExtensionsTests
{
    [Fact]
    public void ToAITool_ConvertsToolCorrectly()
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns("test_tool");
        tool.Description.Returns("A test tool for testing");
        tool.InputSchema.Returns(BinaryData.FromString("""{"type": "object", "properties": {}}"""));

        var aiTool = tool.ToAITool();

        aiTool.Should().NotBeNull();
        aiTool.Should().BeAssignableTo<AITool>();
    }

    [Fact]
    public void ToAITool_PreservesToolName()
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns("my_custom_tool");
        tool.Description.Returns("Description");
        tool.InputSchema.Returns(BinaryData.FromString("""{"type": "object"}"""));

        var aiTool = tool.ToAITool();

        aiTool.Name.Should().Be("my_custom_tool");
    }

    [Fact]
    public void ToAITools_ConvertsCollectionCorrectly()
    {
        var tool1 = Substitute.For<IAgentTool>();
        tool1.Name.Returns("tool1");
        tool1.Description.Returns("First tool");
        tool1.InputSchema.Returns(BinaryData.FromString("""{"type": "object"}"""));

        var tool2 = Substitute.For<IAgentTool>();
        tool2.Name.Returns("tool2");
        tool2.Description.Returns("Second tool");
        tool2.InputSchema.Returns(BinaryData.FromString("""{"type": "object"}"""));

        var tools = new[] { tool1, tool2 };

        var aiTools = tools.ToAITools().ToList();

        aiTools.Should().HaveCount(2);
        aiTools.Should().AllSatisfy(ct => ct.Should().NotBeNull());
    }

    [Fact]
    public void ToAITools_EmptyCollection_ReturnsEmpty()
    {
        var tools = Array.Empty<IAgentTool>();

        var aiTools = tools.ToAITools().ToList();

        aiTools.Should().BeEmpty();
    }

    [Fact]
    public void ToAITools_SingleTool_ReturnsSingleElement()
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns("single_tool");
        tool.Description.Returns("Single tool");
        tool.InputSchema.Returns(BinaryData.FromString("""{"type": "object"}"""));

        var aiTools = new[] { tool }.ToAITools().ToList();

        aiTools.Should().HaveCount(1);
    }
}
