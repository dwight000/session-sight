using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace SessionSight.Agents.Services;

public interface IAIFoundryClientFactory
{
    ChatClient CreateChatClient(string deploymentName);
    EmbeddingClient CreateEmbeddingClient(string deploymentName);
}

public class AIFoundryClientFactory : IAIFoundryClientFactory
{
    private readonly AzureOpenAIClient _openAIClient;

    public AIFoundryClientFactory(IConfiguration config)
    {
        var openAIEndpointStr = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");

        var endpoint = new Uri(openAIEndpointStr);
        var credential = new DefaultAzureCredential();

        _openAIClient = new AzureOpenAIClient(endpoint, credential);
    }

    /// <summary>
    /// Creates a ChatClient for the specified deployment.
    /// Uses Azure OpenAI SDK which works with Cognitive Services OpenAI resources.
    /// </summary>
    public ChatClient CreateChatClient(string deploymentName)
    {
        return _openAIClient.GetChatClient(deploymentName);
    }

    /// <summary>
    /// Creates an EmbeddingClient for the specified deployment.
    /// Uses Azure OpenAI SDK for embedding generation.
    /// </summary>
    public EmbeddingClient CreateEmbeddingClient(string deploymentName)
    {
        return _openAIClient.GetEmbeddingClient(deploymentName);
    }
}
