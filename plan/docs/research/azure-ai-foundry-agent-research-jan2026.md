# Azure AI Foundry Agent SDK Research Summary

**Date:** January 2026
**Status:** Current as of January 21, 2026

---

## Executive Summary

Microsoft has unified its agent development ecosystem under **Microsoft Agent Framework**, which merges AutoGen and Semantic Kernel. The framework supports both **Python and C#/.NET** and integrates with Azure AI Foundry (now branded as **Microsoft Foundry**). Claude models from Anthropic are now available in the Foundry model catalog, making Azure the only cloud offering both OpenAI and Anthropic frontier models.

---

## 1. C#/.NET SDK Support

**Yes, there is full .NET SDK support.**

### NuGet Packages

| Package | Purpose | Current Version |
|---------|---------|-----------------|
| `Azure.AI.Projects` | Main project client for Foundry | 1.1.0 (GA), 1.2.0-beta.3 (preview) |
| `Azure.AI.Agents.Persistent` | Persistent agent service client | 1.2.0-beta.8 |
| `Azure.AI.Inference` | Model inference client | 1.x |
| `Microsoft.Agents.AI` | Agent Framework core | Preview |
| `Microsoft.Agents.AI.AzureAI.Persistent` | Agent Framework + Foundry integration | Preview |

### Installation

```bash
dotnet add package Azure.Identity
dotnet add package Azure.AI.Projects
dotnet add package Azure.AI.Agents.Persistent
dotnet add package Azure.AI.Inference
```

### SDK Architecture

The SDK exposes two client types:

1. **Project Client** - For Foundry-native operations (listing connections, project properties, tracing)
2. **OpenAI-compatible Client** - For functionality building on OpenAI concepts (Responses API, agents, evaluations, fine-tuning)

### SDK Versions

- **2.x preview** - Targets the new Foundry portal and API
- **1.x GA** - Targets Foundry classic

> **Note:** Support for project connection string and hub-based projects has been discontinued. New projects should use project endpoint.

---

## 2. Relationship Between Frameworks

### Microsoft Agent Framework (The Unified Platform)

**Released:** October 2025 (Public Preview)
**GA Target:** Q1 2026

Microsoft Agent Framework is the convergence of:

| Component | Contribution |
|-----------|--------------|
| **Semantic Kernel** | Type-safe skills, state/threads, filters, telemetry, production foundations |
| **AutoGen** | Multi-agent patterns, dynamic orchestration, research prototypes |

### Framework Status

| Framework | Status | Notes |
|-----------|--------|-------|
| **Microsoft Agent Framework** | Active Development (Public Preview) | The future of Microsoft agent development |
| **Semantic Kernel** | Maintenance Mode | Bug fixes, security patches only; no new features |
| **AutoGen** | Maintenance Mode | Bug fixes, security patches only; no new features |

### Four Pillars of Microsoft Agent Framework

1. **Open Standards & Interoperability**
   - Model Context Protocol (MCP) support
   - Agent-to-Agent (A2A) messaging
   - OpenAPI-first design

2. **Pipeline for Research**
   - Experimental orchestration patterns from AutoGen

3. **Extensible by Design**
   - Modular architecture
   - Connectors for Azure AI Foundry, Microsoft Graph, SharePoint, Elastic, Redis

4. **Ready for Production**
   - OpenTelemetry native observability
   - Azure Monitor integration
   - Entra ID authentication
   - CI/CD support

### Azure AI Foundry Agent Service

- **GA Status:** May 2025
- **Runtime:** Fully managed cloud service
- **Languages:** Python, C#, TypeScript, Java (preview)
- **Pricing:** Managed hosting billing starts no earlier than February 1, 2026

---

## 3. Orchestration Patterns Supported

### Built-in Patterns

| Pattern | Description | Implementation |
|---------|-------------|----------------|
| **Sequential** | Chains agents in predefined linear order; each agent processes output from previous | Agent Service native |
| **Concurrent** | Parallelizable analysis tasks; multiple agents work simultaneously | Agent Service native |
| **Group Chat** | Coordinated brainstorming with manager agent | Agent Service native |
| **Handoff** | Transfer control between specialized agents | Agent Service native |
| **Human-in-the-loop** | Requires human approval at certain steps | Workflows feature |
| **Magentic** | Advanced magnetic orchestration | Self-hosted with SDK |

### Foundry Agent Service Workflows (Public Preview)

Announced at Microsoft Ignite, this brings visual and YAML-based orchestration:

- Design, automate, and monitor complex multi-step processes
- Orchestration templates: Sequential, Human-in-the-loop, Group chat
- Checkpointing and time-travel capabilities
- Streaming support

### Anti-Patterns to Avoid

- Sharing mutable state between concurrent agents
- Using complex patterns when simple sequential/concurrent would suffice
- Adding agents without meaningful specialization

---

## 4. Non-Azure LLM Support

### Claude Models on Microsoft Foundry

**Yes, Claude is fully supported.**

| Model | Context Window | Max Output | Best For |
|-------|---------------|------------|----------|
| Claude Opus 4.5 | 200K tokens | 64K | Production code, sophisticated agents, financial analysis |
| Claude Sonnet 4.5 | 200K tokens | - | Real-world agents, complex long-horizon tasks |
| Claude Haiku 4.5 | 200K tokens | - | High-volume use cases, sub-agents |
| Claude Opus 4.1 | 200K tokens | - | Previous generation Opus |

### Key Integration Points

- **MACC Eligible:** Claude works with existing Azure agreements and billing
- **SDK Support:** Access Claude using Python, TypeScript, and C# SDKs
- **Authentication:** Microsoft Entra ID
- **Regional Availability:** Currently East US 2 and West US only

> **Note:** Claude models run on Anthropic's infrastructure; this is a commercial integration for billing and access through Azure.

### Bring Your Own Model (BYO)

The BYO model gateway feature allows:

- Connect external models through AI gateway services (Azure API Management, Mulesoft, Kong)
- Pre/post LLM hooks and policy-based model selection
- Multi-region/multi-provider load-balancing with automatic failover
- Works alongside Foundry's 11,000+ available models

### Full Model Catalog

Over **11,000+ models** including:

- **Microsoft:** Azure OpenAI Service (GPT-5, GPT-4o, o3 for reasoning)
- **Anthropic:** Claude family
- **Mistral AI:** Mistral Large 2411, Ministral 3B, Mistral Nemo
- **Cohere:** Enterprise-grade models
- **Hugging Face:** Open models from the hub
- **Nixtla:** TimeGEN-1 for time series

---

## 5. Local Development Story

### Docker-Based Emulators

#### Durable Task Scheduler (DTS) Emulator

Required for durable agents with state persistence:

```bash
docker pull mcr.microsoft.com/dts/dts-emulator:latest
```

- Dashboard available at `http://localhost:8082`
- Stores conversation history
- Manages agent state across restarts
- Triggers durable orchestrations

### VS Code AI Toolkit Integration

Recent updates bring streamlined development experience:

- Locally create, run, and visualize multi-agent workflows
- Inner dev loop support (build, debug, iterate)
- Works within familiar VS Code environment
- DevContainer configuration available

### Local LLM Support (Ollama)

For fully local development without cloud dependencies:

```python
# Connect to local Ollama instance
model_client = OllamaClient(
    endpoint="http://localhost:11434",
    model="qwen3:8b"
)
```

Supports Qwen, Llama, and other Ollama-compatible models.

### Agents Playground

- Test end-to-end without Microsoft 365 tenant
- Requires tenant only for publishing to Teams/Outlook

### DevUI

Interactive developer interface included in Microsoft Agent Framework:

- Visual workflow debugging
- Agent conversation testing
- OpenTelemetry tracing visualization

---

## Code Examples

### C# - Create a Persistent Agent

```csharp
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

// Create project client
var projectClient = new AIProjectClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")),
    new DefaultAzureCredential()
);

// Get agent client
var agentClient = projectClient.GetPersistentAgentsClient();

// Create agent
var agent = await agentClient.CreateAgentAsync(
    model: "gpt-4o",
    name: "MyAgent",
    instructions: "You are a helpful assistant."
);

// Create thread and run
var thread = await agentClient.CreateThreadAsync();
await agentClient.CreateMessageAsync(thread.Id, "user", "Hello!");
var run = await agentClient.CreateRunAsync(thread.Id, agent.Id);
```

### Python - Create Agent with Claude

```python
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

# Initialize client
client = AIProjectClient(
    endpoint=os.environ["AZURE_AI_PROJECT_ENDPOINT"],
    credential=DefaultAzureCredential()
)

# Create agent using Claude
agent = client.agents.create_agent(
    model="claude-sonnet-4-5",  # Use Claude model
    name="Claude Assistant",
    instructions="You are a helpful assistant."
)
```

---

## Migration Path

For existing Semantic Kernel or AutoGen users:

1. **AutoGen users:** Migrate multi-agent patterns to Microsoft Agent Framework
2. **Semantic Kernel users:** Core concepts (plugins, filters, planning) carry forward
3. **Both:** Single SDK now handles agents, tools, and workflows

> Microsoft Agent Framework is the "next generation of both Semantic Kernel and Autogen" with clear migration paths for existing code.

---

## Enterprise Adoption

Companies using Microsoft Agent Framework:

- **KPMG:** Automating audit testing and documentation
- **BMW:** Multi-agent systems analyzing vehicle telemetry
- **Commerzbank:** Avatar-driven customer support
- **Fujitsu, Citrix, TCS, TeamViewer, Elastic:** Various implementations

Over **10,000 organizations** already using Azure AI Foundry Agent Service.

---

## Key Links and Documentation

### Official Documentation

- [Microsoft Foundry SDK Overview](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/sdk-overview?view=foundry-classic)
- [Azure AI Foundry Agent Service Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/)
- [Microsoft Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)
- [Azure AI Foundry Agents in Agent Framework](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-types/azure-ai-foundry-agent)
- [What's New in Foundry Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/whats-new?view=foundry-classic)

### Claude Integration

- [Deploy and Use Claude Models in Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-models/how-to/use-foundry-models-claude?view=foundry-classic)
- [Claude in Microsoft Foundry - Anthropic Docs](https://platform.claude.com/docs/en/build-with-claude/claude-in-microsoft-foundry)
- [Introducing Claude in Microsoft Foundry - Azure Blog](https://azure.microsoft.com/en-us/blog/introducing-anthropics-claude-models-in-microsoft-foundry-bringing-frontier-intelligence-to-azure/)

### GitHub Repositories

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [Get Started with AI Agents Sample](https://github.com/Azure-Samples/get-started-with-ai-agents)

### NuGet Packages

- [Azure.AI.Projects](https://www.nuget.org/packages/Azure.AI.Projects/)
- [Azure.AI.Agents.Persistent](https://www.nuget.org/packages/Azure.AI.Agents.Persistent/)

### Architecture Guidance

- [AI Agent Orchestration Patterns](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns)
- [Multi-Agent Workflow Automation Architecture](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/idea/multiple-agent-workflow-automation)

### Workshops

- [Build Your First Agent Workshop](https://microsoft.github.io/build-your-first-agent-with-azure-ai-agent-service-workshop/)

### Blog Posts

- [Introducing Microsoft Agent Framework](https://devblogs.microsoft.com/foundry/introducing-microsoft-agent-framework-the-open-source-engine-for-agentic-ai-apps/)
- [Multi-Agent Workflows in Foundry Agent Service](https://devblogs.microsoft.com/foundry/introducing-multi-agent-workflows-in-foundry-agent-service/)
- [Building AI Agents with A2A .NET SDK](https://devblogs.microsoft.com/foundry/building-ai-agents-a2a-dotnet-sdk/)

---

## Summary Table

| Question | Answer |
|----------|--------|
| C#/.NET SDK? | **Yes** - Full support via `Azure.AI.Projects`, `Azure.AI.Agents.Persistent`, and `Microsoft.Agents.AI` |
| Python SDK? | **Yes** - Full support via `agent-framework` and `azure-ai-projects` |
| Claude support? | **Yes** - Claude Opus 4.5, Sonnet 4.5, Haiku 4.5, Opus 4.1 available |
| Non-Azure LLMs? | **Yes** - 11,000+ models including Anthropic, Mistral, Cohere, Hugging Face; plus BYO model gateway |
| Orchestration patterns? | **Sequential, Concurrent, Group Chat, Handoff, Human-in-the-loop, Magentic** |
| Local development? | **Yes** - DTS emulator, VS Code AI Toolkit, Ollama support, DevUI, Agents Playground |
| AutoGen relationship? | Merged into Microsoft Agent Framework; AutoGen in maintenance mode |
| Semantic Kernel relationship? | Merged into Microsoft Agent Framework; SK in maintenance mode |

---

*Last updated: January 21, 2026*
