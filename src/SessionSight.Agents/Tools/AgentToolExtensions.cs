using OpenAI.Chat;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Extension methods for converting agent tools to OpenAI ChatTool format.
/// </summary>
public static class AgentToolExtensions
{
    /// <summary>
    /// Converts an <see cref="IAgentTool"/> to an OpenAI <see cref="ChatTool"/>.
    /// </summary>
    public static ChatTool ToChatTool(this IAgentTool tool)
    {
        return ChatTool.CreateFunctionTool(
            tool.Name,
            tool.Description,
            tool.InputSchema);
    }

    /// <summary>
    /// Converts a collection of agent tools to OpenAI ChatTools.
    /// </summary>
    public static IEnumerable<ChatTool> ToChatTools(this IEnumerable<IAgentTool> tools)
    {
        return tools.Select(t => t.ToChatTool());
    }
}
