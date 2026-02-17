using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SessionSight.Core.Resilience;

/// <summary>
/// Holds named CircuitBreakerState instances for each Azure service.
/// </summary>
public sealed class CircuitBreakerRegistry
{
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _breakers = new();
    private readonly ILoggerFactory? _loggerFactory;

    public CircuitBreakerRegistry(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    public CircuitBreakerState Get(string serviceName)
    {
        return _breakers.GetOrAdd(serviceName, name =>
            new CircuitBreakerState(
                name,
                logger: _loggerFactory?.CreateLogger<CircuitBreakerState>()));
    }
}
