namespace SessionSight.Agents.Services;

public sealed class PipelineDiagnosticsOptions
{
    public const string SectionName = "PipelineDiagnostics";
    public bool StoreLlmTraces { get; init; }
}
