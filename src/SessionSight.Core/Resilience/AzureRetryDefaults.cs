using Azure.Core;
using System.ClientModel.Primitives;

namespace SessionSight.Core.Resilience;

public static class AzureRetryDefaults
{
    public const int MaxRetries = 5;
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);
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
        options.RetryPolicy = new ClientRetryPolicy(MaxRetries);
        return options;
    }
}
