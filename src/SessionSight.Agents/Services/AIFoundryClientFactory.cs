using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using SessionSight.Agents.Routing;
using SessionSight.Core.Resilience;

namespace SessionSight.Agents.Services;

public interface IAIFoundryClientFactory
{
    IChatClient CreateChatClient(ModelSelection selection);
    EmbeddingClient CreateEmbeddingClient(string deploymentName);
}

public partial class AIFoundryClientFactory : IAIFoundryClientFactory
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AIFoundryClientFactory> _logger;
    private readonly CircuitBreakerRegistry _circuitBreakerRegistry;
    private AzureOpenAIClient? _aiServicesClient;
    private readonly object _aiServicesLock = new();

    public AIFoundryClientFactory(IConfiguration config, ILogger<AIFoundryClientFactory> logger, CircuitBreakerRegistry circuitBreakerRegistry)
    {
        _config = config;
        _logger = logger;
        _circuitBreakerRegistry = circuitBreakerRegistry;

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
    /// Creates an IChatClient for the specified model selection.
    /// Routes to the correct Azure endpoint based on the provider.
    /// </summary>
    public IChatClient CreateChatClient(ModelSelection selection) => selection.Provider switch
    {
        ModelProvider.AzureOpenAI => _openAIClient
            .GetChatClient(selection.DeploymentName).AsIChatClient(),
        ModelProvider.AzureAIServices => GetAIServicesClient()
            .GetChatClient(selection.DeploymentName).AsIChatClient(),
        _ => throw new NotSupportedException($"Unknown provider: {selection.Provider}")
    };

    /// <summary>
    /// Creates an EmbeddingClient for the specified deployment.
    /// Embeddings are always served from the Azure OpenAI endpoint.
    /// </summary>
    public EmbeddingClient CreateEmbeddingClient(string deploymentName)
    {
        return _openAIClient.GetEmbeddingClient(deploymentName);
    }

    private AzureOpenAIClient GetAIServicesClient()
    {
        if (_aiServicesClient is not null)
            return _aiServicesClient;

        lock (_aiServicesLock)
        {
            if (_aiServicesClient is not null)
                return _aiServicesClient;

            var endpointStr = _config["AzureAIServices:Endpoint"];
            if (string.IsNullOrWhiteSpace(endpointStr))
                throw new InvalidOperationException(
                    "AzureAIServices:Endpoint not configured. Required for non-OpenAI models (e.g. Mistral-Large-3).");

            var endpoint = new Uri(endpointStr);
            var credential = new DefaultAzureCredential();
            var breaker = _circuitBreakerRegistry.Get("aiservices");
            var options = AzureRetryDefaults.ConfigureRetryPolicy(
                new AzureOpenAIClientOptions(), _logger, breaker, "aiservices");

            _aiServicesClient = new AzureOpenAIClient(endpoint, credential, options);
            LogAIServicesClientCreated(_logger, endpointStr);
            return _aiServicesClient;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AIFoundryClientFactory configured with retry: MaxRetries={MaxRetries}, Delay={Delay}, MaxDelay={MaxDelay}")]
    private static partial void LogRetryConfiguration(ILogger logger, int maxRetries, TimeSpan delay, TimeSpan maxDelay);

    [LoggerMessage(Level = LogLevel.Information, Message = "AIServices client created for endpoint {Endpoint}")]
    private static partial void LogAIServicesClientCreated(ILogger logger, string endpoint);
}
