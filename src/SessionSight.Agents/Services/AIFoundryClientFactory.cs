using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace SessionSight.Agents.Services;

public interface IAIFoundryClientFactory
{
    PersistentAgentsClient CreateClient();
}

public class AIFoundryClientFactory : IAIFoundryClientFactory
{
    private readonly string _projectEndpoint;

    public AIFoundryClientFactory(IConfiguration config)
    {
        _projectEndpoint = config["AIFoundry:ProjectEndpoint"]
            ?? throw new InvalidOperationException("AIFoundry:ProjectEndpoint not configured");
    }

    public PersistentAgentsClient CreateClient()
        => new(_projectEndpoint, new DefaultAzureCredential());
}
