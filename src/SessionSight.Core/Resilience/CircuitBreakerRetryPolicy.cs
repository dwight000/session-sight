using Microsoft.Extensions.Logging;
using SessionSight.Core.Exceptions;
using System.ClientModel.Primitives;

namespace SessionSight.Core.Resilience;

/// <summary>
/// Retry policy with circuit breaker integration for System.ClientModel clients.
/// Records failures/successes via OnRequestSent hooks and throws
/// CircuitBreakerOpenException via OnSendingRequest when circuit is open.
/// </summary>
public sealed partial class CircuitBreakerRetryPolicy : ClientRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeSpan _jitter;
    private readonly CircuitBreakerState _breaker;
    private readonly string _serviceName;
    private readonly ILogger? _logger;

    public CircuitBreakerRetryPolicy(
        int maxRetries,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        TimeSpan jitter,
        CircuitBreakerState breaker,
        string serviceName,
        ILogger? logger = null)
        : base(maxRetries)
    {
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
        _jitter = jitter;
        _breaker = breaker;
        _serviceName = serviceName;
        _logger = logger;
    }

    protected override void OnSendingRequest(PipelineMessage message)
    {
        if (_breaker.IsOpen(out var remaining))
            throw new CircuitBreakerOpenException(_serviceName, remaining);
    }

    protected override ValueTask OnSendingRequestAsync(PipelineMessage message)
    {
        if (_breaker.IsOpen(out var remaining))
            throw new CircuitBreakerOpenException(_serviceName, remaining);
        return default;
    }

    protected override void OnRequestSent(PipelineMessage message)
    {
        RecordOutcome(message);
    }

    protected override ValueTask OnRequestSentAsync(PipelineMessage message)
    {
        RecordOutcome(message);
        return default;
    }

    protected override bool ShouldRetry(PipelineMessage message, Exception? exception)
    {
        if (message.Response is { } response)
        {
            var status = response.Status;
            if (status == 429)
            {
                if (_logger is not null) LogRateLimitRetry(_logger, status);
                return true;
            }
            if (status >= 500 && status < 600)
            {
                if (_logger is not null) LogServerErrorRetry(_logger, status);
                return true;
            }
            return false;
        }
        return exception is not null;
    }

    protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount)
    {
        var exponential = TimeSpan.FromTicks(_baseDelay.Ticks * (1L << (tryCount - 1)));
        var delay = exponential > _maxDelay ? _maxDelay : exponential;
        var jitterMs = (Random.Shared.NextDouble() * 2 - 1) * _jitter.TotalMilliseconds;
        var finalDelay = delay + TimeSpan.FromMilliseconds(jitterMs);
        if (_logger is not null) LogRetryDelay(_logger, tryCount, finalDelay.TotalSeconds);
        return finalDelay;
    }

    private void RecordOutcome(PipelineMessage message)
    {
        if (message.Response is { } response && response.Status >= 500)
            _breaker.RecordFailure();
        else
            _breaker.RecordSuccess();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Circuit breaker: rate limited (HTTP {Status}), will retry")]
    private static partial void LogRateLimitRetry(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Circuit breaker: server error (HTTP {Status}), will retry")]
    private static partial void LogServerErrorRetry(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker: retry attempt {TryCount}, waiting {DelaySeconds:F1}s")]
    private static partial void LogRetryDelay(ILogger logger, int tryCount, double delaySeconds);
}
