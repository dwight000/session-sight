namespace SessionSight.Agents.Services;

public interface IExtractionJobDispatcher
{
    ValueTask EnqueueAsync(Guid sessionId, string? jobKey = null);
}
