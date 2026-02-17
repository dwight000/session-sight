using Microsoft.Extensions.Logging;

namespace SessionSight.Core.Resilience;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Thread-safe circuit breaker state machine.
/// Closed → Open (after threshold failures) → HalfOpen (after break duration) → Closed (on success) or Open (on failure).
/// </summary>
public sealed partial class CircuitBreakerState
{
    private readonly object _lock = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _failureWindow;
    private readonly TimeSpan _breakDuration;
    private readonly string _serviceName;
    private readonly ILogger? _logger;

    private readonly Queue<DateTimeOffset> _failures = new();
    private CircuitState _state = CircuitState.Closed;
    private DateTimeOffset _openedAt;

    public CircuitBreakerState(
        string serviceName,
        int failureThreshold = 5,
        TimeSpan? failureWindow = null,
        TimeSpan? breakDuration = null,
        ILogger? logger = null)
    {
        _serviceName = serviceName;
        _failureThreshold = failureThreshold;
        _failureWindow = failureWindow ?? TimeSpan.FromSeconds(30);
        _breakDuration = breakDuration ?? TimeSpan.FromSeconds(60);
        _logger = logger;
    }

    public CircuitState State
    {
        get { lock (_lock) return _state; }
    }

    public bool IsOpen(out TimeSpan remaining)
    {
        lock (_lock)
        {
            if (_state == CircuitState.Closed)
            {
                remaining = TimeSpan.Zero;
                return false;
            }

            var elapsed = DateTimeOffset.UtcNow - _openedAt;
            if (elapsed >= _breakDuration)
            {
                _state = CircuitState.HalfOpen;
                if (_logger is not null)
                    LogHalfOpen(_logger, _serviceName);
                remaining = TimeSpan.Zero;
                return false;
            }

            remaining = _breakDuration - elapsed;
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _failures.Clear();
                if (_logger is not null)
                    LogCircuitClosed(_logger, _serviceName);
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;

            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Open;
                _openedAt = now;
                if (_logger is not null)
                    LogCircuitReopened(_logger, _serviceName);
                return;
            }

            if (_state == CircuitState.Open)
                return;

            // Closed state — track failures in sliding window
            _failures.Enqueue(now);
            var windowStart = now - _failureWindow;
            while (_failures.Count > 0 && _failures.Peek() < windowStart)
                _failures.Dequeue();

            if (_failures.Count >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = now;
                if (_logger is not null)
                    LogCircuitOpened(_logger, _serviceName, _failures.Count, _failureWindow.TotalSeconds);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Circuit breaker OPENED for {ServiceName}: {FailureCount} failures in {WindowSeconds}s")]
    private static partial void LogCircuitOpened(ILogger logger, string serviceName, int failureCount, double windowSeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker HALF-OPEN for {ServiceName}: allowing test request")]
    private static partial void LogHalfOpen(ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker CLOSED for {ServiceName}: test request succeeded")]
    private static partial void LogCircuitClosed(ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Circuit breaker RE-OPENED for {ServiceName}: test request failed")]
    private static partial void LogCircuitReopened(ILogger logger, string serviceName);
}
