using Azure.Core;
using Microsoft.Extensions.Logging;
using System.ClientModel.Primitives;

namespace SessionSight.Core.Resilience;

/// <summary>
/// Retry configuration tuned for Azure OpenAI rate limits.
///
/// Timing rationale:
/// - Azure OpenAI 429s typically say "retry after 45-54 seconds"
/// - Retry 1 at 7s: catches quick transient errors early
/// - Retry 2 at 21s: ~half of 45s, may clear if limit triggered earlier in request
/// - Retry 3 at 49s: just over 45s, clears most rate limit windows
/// - ±1s jitter per retry prevents thundering herd, total range 46s-52s
/// </summary>
public static class AzureRetryDefaults
{
    public const int MaxRetries = 3;
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(7);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan Jitter = TimeSpan.FromSeconds(1);

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
    public static T ConfigureRetryPolicy<T>(T options, ILogger? logger = null) where T : ClientPipelineOptions
    {
        options.RetryPolicy = new SpacedRetryPolicy(MaxRetries, Delay, MaxDelay, Jitter, logger);
        return options;
    }
}

/// <summary>
/// Retry policy with exponential backoff and jitter for System.ClientModel clients.
/// Delays: 7s, 14s, 28s ±1s each → total 49s ±3s (range 46s-52s).
/// Designed to clear 45s rate limit windows by retry 3.
/// </summary>
public sealed partial class SpacedRetryPolicy : ClientRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeSpan _jitter;
    private readonly ILogger? _logger;

    public SpacedRetryPolicy(int maxRetries, TimeSpan baseDelay, TimeSpan maxDelay, TimeSpan jitter, ILogger? logger = null)
        : base(maxRetries)
    {
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
        _jitter = jitter;
        _logger = logger;
    }

    protected override bool ShouldRetry(PipelineMessage message, Exception? exception)
    {
        // Retry on 429 (TooManyRequests) and 5xx server errors
        if (message.Response is { } response)
        {
            var status = response.Status;
            if (status == 429)
            {
                if (_logger is not null)
                {
                    LogRateLimitRetry(_logger, status);
                }
                return true;
            }
            if (status >= 500 && status < 600)
            {
                if (_logger is not null)
                {
                    LogServerErrorRetry(_logger, status);
                }
                return true;
            }
            return false;
        }

        // Retry on transient exceptions (network errors, timeouts)
        if (exception is not null)
        {
            if (_logger is not null)
            {
                LogExceptionRetry(_logger, exception.GetType().Name, exception.Message);
            }
            return true;
        }
        return false;
    }

    protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount)
    {
        // Exponential backoff: 7s, 14s, 28s
        var exponential = TimeSpan.FromTicks(_baseDelay.Ticks * (1L << (tryCount - 1)));
        var delay = exponential > _maxDelay ? _maxDelay : exponential;

        // Add ±1s jitter
        var jitterMs = (Random.Shared.NextDouble() * 2 - 1) * _jitter.TotalMilliseconds;
        var finalDelay = delay + TimeSpan.FromMilliseconds(jitterMs);

        if (_logger is not null)
        {
            LogRetryDelay(_logger, tryCount, finalDelay.TotalSeconds);
        }

        return finalDelay;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure OpenAI rate limited (HTTP {Status}), will retry")]
    private static partial void LogRateLimitRetry(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure OpenAI server error (HTTP {Status}), will retry")]
    private static partial void LogServerErrorRetry(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure OpenAI transient error ({ExceptionType}: {ExceptionMessage}), will retry")]
    private static partial void LogExceptionRetry(ILogger logger, string exceptionType, string exceptionMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retry attempt {TryCount}, waiting {DelaySeconds:F1}s")]
    private static partial void LogRetryDelay(ILogger logger, int tryCount, double delaySeconds);
}
