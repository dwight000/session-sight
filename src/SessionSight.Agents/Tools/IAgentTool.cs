namespace SessionSight.Agents.Tools;

/// <summary>
/// Interface for tools that can be called by agents during their reasoning loop.
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// The unique name of the tool (used in function calls).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the tool does (shown to the LLM).
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON schema describing the input parameters.
    /// </summary>
    BinaryData InputSchema { get; }

    /// <summary>
    /// Executes the tool with the given input.
    /// </summary>
    /// <param name="input">The input as JSON.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the tool execution.</returns>
    Task<ToolResult> ExecuteAsync(BinaryData input, CancellationToken ct = default);
}

/// <summary>
/// Result of executing an agent tool.
/// </summary>
/// <param name="Success">Whether the tool executed successfully.</param>
/// <param name="Data">The result data as JSON.</param>
/// <param name="ErrorMessage">Error message if execution failed.</param>
public record ToolResult(
    bool Success,
    BinaryData Data,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful result with the given data.
    /// </summary>
    public static ToolResult Ok<T>(T data) =>
        new(true, BinaryData.FromObjectAsJson(data));

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static ToolResult Error(string message) =>
        new(false, BinaryData.FromObjectAsJson(new { error = message }), message);
}
