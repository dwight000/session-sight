namespace SessionSight.Core.Exceptions;

public class CircuitBreakerOpenException : AzureServiceException
{
    public TimeSpan RetryAfter { get; }

    public CircuitBreakerOpenException(string serviceName, TimeSpan retryAfter)
        : base($"Circuit breaker is open for {serviceName}. Retry after {retryAfter.TotalSeconds:F0}s.")
    {
        RetryAfter = retryAfter;
    }
}
