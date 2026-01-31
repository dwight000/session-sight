namespace SessionSight.Agents.Agents;

/// <summary>
/// Base interface for all SessionSight agents.
/// </summary>
public interface ISessionSightAgent
{
    /// <summary>
    /// Human-readable name of the agent.
    /// </summary>
    string Name { get; }
}
