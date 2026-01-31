var builder = DistributedApplication.CreateBuilder(args);

// Azure OpenAI resource — uses existing resource in Azure via connection string
var openai = builder.AddAzureOpenAI("openai");
openai.AddDeployment("gpt-4o", "gpt-4o", "2024-08-06");

// Agent spike console app — references OpenAI for service discovery
var agentSpike = builder.AddProject<Projects.AgentSpike>("agent-spike")
    .WithReference(openai);

builder.Build().Run();
