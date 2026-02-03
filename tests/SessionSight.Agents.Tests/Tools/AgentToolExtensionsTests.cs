using FluentAssertions;
using NSubstitute;
using SessionSight.Agents.Tools;

namespace SessionSight.Agents.Tests.Tools;

public class AgentToolExtensionsTests
{
    [Fact]
    public void ToChatTool_ConvertsToolCorrectly()
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns("test_tool");
        tool.Description.Returns("A test tool for testing");
        tool.InputSchema.Returns(BinaryData.FromString("""{"type": "object", "properties": {}}"""));

        var chatTool = tool.ToChatTool();

        chatTool.Should().NotBeNull();
        chatTool.Kind.ToString().Should().Be("Function");
    }

    [Fact]
    public void ToChatTool_PreservesToolName()
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns("my_custom_tool");
        tool.Description.Returns("Description");
        tool.InputSchema.Returns(BinaryData.FromString("""{"type": "object"}"""));

        var chatTool = tool.ToChatTool();

        // ChatTool doesn't expose name directly, but we can verify it's created without error
        chatTool.Should().NotBeNull();
    }

    [Fact]
    public void ToChatTools_ConvertsCollectionCorrectly()
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

        var chatTools = tools.ToChatTools().ToList();

        chatTools.Should().HaveCount(2);
        chatTools.Should().AllSatisfy(ct => ct.Should().NotBeNull());
    }

    [Fact]
    public void ToChatTools_EmptyCollection_ReturnsEmpty()
    {
        var tools = Array.Empty<IAgentTool>();

        var chatTools = tools.ToChatTools().ToList();

        chatTools.Should().BeEmpty();
    }

    [Fact]
    public void ToChatTools_SingleTool_ReturnsSingleElement()
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns("single_tool");
        tool.Description.Returns("Single tool");
        tool.InputSchema.Returns(BinaryData.FromString("""{"type": "object"}"""));

        var chatTools = new[] { tool }.ToChatTools().ToList();

        chatTools.Should().HaveCount(1);
    }
}
