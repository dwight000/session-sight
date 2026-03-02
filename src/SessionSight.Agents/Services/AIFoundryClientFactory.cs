using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using SessionSight.Core.Resilience;

namespace SessionSight.Agents.Services;

public interface IAIFoundryClientFactory
{
    IChatClient CreateChatClient(string deploymentName);
    EmbeddingClient CreateEmbeddingClient(string deploymentName);
}

public partial class AIFoundryClientFactory : IAIFoundryClientFactory
{
    private readonly AzureOpenAIClient _openAIClient;

    public AIFoundryClientFactory(IConfiguration config, ILogger<AIFoundryClientFactory> logger, CircuitBreakerRegistry circuitBreakerRegistry)
    {
        var openAIEndpointStr = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");

        var endpoint = new Uri(openAIEndpointStr);
        var credential = new DefaultAzureCredential();
        var breaker = circuitBreakerRegistry.Get("openai");
        var options = AzureRetryDefaults.ConfigureRetryPolicy(new AzureOpenAIClientOptions(), logger, breaker, "openai");

        _openAIClient = new AzureOpenAIClient(endpoint, credential, options);

        LogRetryConfiguration(logger, AzureRetryDefaults.MaxRetries, AzureRetryDefaults.Delay, AzureRetryDefaults.MaxDelay);
    }

    /// <summary>
    /// Creates an IChatClient for the specified deployment.
    /// Uses Azure OpenAI SDK under the hood, exposed via M.E.AI abstraction.
    /// </summary>
    public IChatClient CreateChatClient(string deploymentName)
    {
        return _openAIClient.GetChatClient(deploymentName).AsIChatClient();
    }

    /// <summary>
    /// Creates an EmbeddingClient for the specified deployment.
    /// Uses Azure OpenAI SDK for embedding generation.
    /// </summary>
    public EmbeddingClient CreateEmbeddingClient(string deploymentName)
    {
        return _openAIClient.GetEmbeddingClient(deploymentName);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AIFoundryClientFactory configured with retry: MaxRetries={MaxRetries}, Delay={Delay}, MaxDelay={MaxDelay}")]
    private static partial void LogRetryConfiguration(ILogger logger, int maxRetries, TimeSpan delay, TimeSpan maxDelay);
}
