using Azure.AI.Agents.Persistent;
using Azure.AI.Inference;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace SessionSight.Agents.Services;

public interface IAIFoundryClientFactory
{
    PersistentAgentsClient CreateAgentClient();
    ChatCompletionsClient CreateChatClient();
}

public class AIFoundryClientFactory : IAIFoundryClientFactory
{
    private readonly string _projectEndpoint;

    public AIFoundryClientFactory(IConfiguration config)
    {
        _projectEndpoint = config["AIFoundry:ProjectEndpoint"]
            ?? throw new InvalidOperationException("AIFoundry:ProjectEndpoint not configured");
    }

    public PersistentAgentsClient CreateAgentClient()
        => new(_projectEndpoint, new DefaultAzureCredential());

    public ChatCompletionsClient CreateChatClient()
    {
        var projectClient = new AIProjectClient(
            new Uri(_projectEndpoint),
            new DefaultAzureCredential());
        return projectClient.GetChatCompletionsClient();
    }
}
