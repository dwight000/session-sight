namespace SessionSight.Api.Middleware;

public sealed class RequestResponseLoggingOptions
{
    public const string SectionName = "RequestResponseLogging";

    public bool Enabled { get; init; } = true;

    public bool LogBodies { get; init; }

    public int? MaxBodyLogBytes { get; init; }
}
