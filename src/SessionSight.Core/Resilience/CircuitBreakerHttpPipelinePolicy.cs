using Azure.Core;
using Azure.Core.Pipeline;
using SessionSight.Core.Exceptions;

namespace SessionSight.Core.Resilience;

/// <summary>
/// Azure.Core HttpPipelinePolicy that short-circuits requests when the circuit breaker is open.
/// Added as a PerCall policy so it runs before retries.
/// </summary>
public sealed class CircuitBreakerHttpPipelinePolicy : HttpPipelinePolicy
{
    private readonly CircuitBreakerState _state;
    private readonly string _serviceName;

    public CircuitBreakerHttpPipelinePolicy(CircuitBreakerState state, string serviceName)
    {
        _state = state;
        _serviceName = serviceName;
    }

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        if (_state.IsOpen(out var remaining))
            throw new CircuitBreakerOpenException(_serviceName, remaining);

        ProcessNext(message, pipeline);
        RecordOutcome(message);
    }

    public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        if (_state.IsOpen(out var remaining))
            throw new CircuitBreakerOpenException(_serviceName, remaining);

        await ProcessNextAsync(message, pipeline);
        RecordOutcome(message);
    }

    private void RecordOutcome(HttpMessage message)
    {
        if (message.HasResponse && message.Response.Status >= 500)
            _state.RecordFailure();
        else
            _state.RecordSuccess();
    }
}
