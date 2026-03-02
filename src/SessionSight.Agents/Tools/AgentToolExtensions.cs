using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SessionSight.Agents.Tools;

/// <summary>
/// Extension methods for converting agent tools to M.E.AI AITool format.
/// </summary>
public static class AgentToolExtensions
{
    /// <summary>
    /// Converts an <see cref="IAgentTool"/> to an <see cref="AITool"/> declaration.
    /// </summary>
    public static AITool ToAITool(this IAgentTool tool)
    {
        using var doc = JsonDocument.Parse(tool.InputSchema);
        return AIFunctionFactory.CreateDeclaration(
            tool.Name,
            tool.Description,
            doc.RootElement.Clone());
    }

    /// <summary>
    /// Converts a collection of agent tools to AITools.
    /// </summary>
    public static IEnumerable<AITool> ToAITools(this IEnumerable<IAgentTool> tools)
    {
        return tools.Select(t => t.ToAITool());
    }
}
