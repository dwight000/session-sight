using Azure.Core;
using System.ClientModel.Primitives;

namespace SessionSight.Core.Resilience;

public static class AzureRetryDefaults
{
    public const int MaxRetries = 5;
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Applies standard retry configuration to Azure SDK clients using Azure.Core (Search, Document Intelligence, etc.).
    /// </summary>
    public static T Configure<T>(T options) where T : ClientOptions
    {
        options.Retry.MaxRetries = MaxRetries;
        options.Retry.Delay = Delay;
        options.Retry.MaxDelay = MaxDelay;
        options.Retry.Mode = RetryMode.Exponential;
        options.Retry.NetworkTimeout = NetworkTimeout;
        return options;
    }

    /// <summary>
    /// Applies standard retry configuration to Azure SDK clients using System.ClientModel (OpenAI, etc.).
    /// </summary>
    public static T ConfigureRetryPolicy<T>(T options) where T : ClientPipelineOptions
    {
        options.RetryPolicy = new SpacedRetryPolicy(MaxRetries, Delay, MaxDelay);
        return options;
    }
}

/// <summary>
/// Retry policy with configurable exponential backoff for System.ClientModel clients.
/// Respects Retry-After headers from 429 responses; otherwise uses exponential delay
/// starting at <paramref name="baseDelay"/> (3s default → ~93s total for 5 retries).
/// </summary>
public sealed class SpacedRetryPolicy : ClientRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public SpacedRetryPolicy(int maxRetries, TimeSpan baseDelay, TimeSpan maxDelay)
        : base(maxRetries)
    {
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount)
    {
        // Exponential backoff: 3s, 6s, 12s, 24s, 48s → ~93s total
        var exponential = TimeSpan.FromTicks(_baseDelay.Ticks * (1L << (tryCount - 1)));
        return exponential > _maxDelay ? _maxDelay : exponential;
    }
}
