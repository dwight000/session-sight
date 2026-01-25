# .NET Aspire AI Capabilities Research Summary (January 2026)

## 1. Current Version and Key Features

### Latest Version: Aspire 13.1.0

As of January 2026, **Aspire 13.1.0** is the latest release. The product has been rebranded from ".NET Aspire" to simply **"Aspire"**, reflecting its evolution into a full polyglot application platform.

**Major Version Timeline:**
- **Aspire 9.0** (November 2024): Initial major AI integrations
- **Aspire 9.5** (September 2025): GenAI Visualizer, expanded AI support
- **Aspire 13.0** (2025): Polyglot support for Python/JavaScript
- **Aspire 13.1.0** (Current): Latest servicing release

**Key Features in Aspire 13.x:**
- First-class Python and JavaScript support (polyglot orchestration)
- Container files as build artifacts
- Automatic development certificate trust across languages
- Enhanced AI integrations and GenAI telemetry visualization
- Single-file AppHost option

---

## 2. Built-in Components for AI Workloads

### Azure AI Services

| Component | NuGet Package | Description |
|-----------|---------------|-------------|
| **Azure AI Foundry** | `Aspire.Hosting.Azure.AIFoundry`, `Aspire.Azure.AI.Inference` | Connect to Azure AI Foundry or run locally with Foundry Local |
| **Azure AI Search** | `Aspire.Hosting.Azure.Search` | Vector search, hybrid search, and semantic ranking |
| **Azure OpenAI** | `Aspire.Azure.AI.OpenAI` | First-class OpenAI/Azure OpenAI integration |
| **GitHub Models** | Built-in integration | Access GPT, DeepSeek, Phi models via GitHub |

**Azure AI Foundry Example:**
```csharp
// In AppHost
var foundry = builder.AddAzureAIFoundry("foundry");
builder.AddProject<Projects.ExampleProject>().WithReference(foundry);

// In service project
builder.AddAzureChatCompletionsClient("foundry");
```

### Vector Databases

| Database | Package | Key Features |
|----------|---------|--------------|
| **Azure AI Search** | `Aspire.Hosting.Azure.Search` | Enterprise-grade, hybrid search, semantic ranking |
| **Qdrant** | `Aspire.Hosting.Qdrant` | Open-source, Rust-based, includes web UI |
| **Milvus** | `Aspire.Hosting.Milvus` | Cloud-native, GPU acceleration, billion-scale vectors |
| **Chroma** | Community Toolkit | Simple RAG setups |

**Qdrant Example:**
```csharp
var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
```

### LLM Orchestration

**OpenAI Integration (Aspire 9.5+):**
```csharp
var openai = builder.AddOpenAI("openai");
builder.AddProject<Projects.MyApp>().WithReference(openai);
```

**Ollama (Community Toolkit) for Local LLMs:**
```csharp
var ollama = builder.AddOllama("ollama", 11434)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithOpenWebUI();

var llama = ollama.AddModel("llama3.2");
```

---

## 3. Service Discovery, Observability, and Local Development

### Service Discovery for AI Agents

Aspire provides built-in service discovery through `Microsoft.Extensions.ServiceDiscovery`:

```csharp
// AppHost configuration
var aiService = builder.AddProject<Projects.AIAgentService>("ai-agent");
var frontend = builder.AddProject<Projects.Frontend>("frontend")
    .WithReference(aiService);
```

**Key Capabilities:**
- Dynamic endpoint resolution by service name
- Automatic configuration injection via `WithReference()`
- Works with HTTP, gRPC, and custom protocols
- No fixed URLs required - services locate each other at runtime

### Observability / Telemetry for AI Calls

**GenAI Visualizer (Aspire 9.5+):**
The Aspire Dashboard includes a specialized **GenAI Telemetry Visualizer**:
- Collates and visualizes LLM-centric calls
- Shows prompts, responses, and images returned from LLMs
- Sparkle icon indicates AI telemetry presence
- JSON/XML payloads are highlighted and formatted

**OpenTelemetry Integration:**
- Automatic logging, tracing, and metrics setup
- Follows OpenTelemetry GenAI Semantic Conventions
- Works with Semantic Kernel and Microsoft Agent Framework

**Enable Detailed AI Telemetry:**
```csharp
AppContext.SetSwitch(
    "Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive",
    true);
```

### Local Development with AI Services

**Ollama for Local LLMs:**
```csharp
var ollama = builder.AddOllama("ollama")
    .WithDataVolume()  // Persist models across restarts
    .WithOpenWebUI();   // Include web interface

var model = ollama.AddModel("deepseek-r1");
```

**Foundry Local (Azure AI Foundry Emulator):**
```csharp
var foundry = builder.AddAzureAIFoundry("foundry")
    .RunAsFoundryLocal();  // Downloads and runs models locally
```

**Development-to-Production Transition:**
- Use `Microsoft.Extensions.AI` abstractions for provider-agnostic code
- Switch from Ollama (local) to Azure OpenAI (cloud) at deployment
- No code changes required, just configuration

---

## 4. Semantic Kernel and Azure AI Foundry Integration

### Semantic Kernel

Semantic Kernel integrates naturally with Aspire for building AI agents.

**Microsoft Agent Framework (October 2025):**
- Unifies Semantic Kernel and AutoGen into a single API surface
- Supports OpenAI, Azure OpenAI, Anthropic Claude, Ollama
- Native integration with Aspire for cloud-native AI deployment
- Full MCP (Model Context Protocol) support

**Sample Setup:**
```csharp
// AppHost
var openai = builder.AddConnectionString("openai");
var aiBackend = builder.AddProject<Projects.AIBackend>("ai-backend")
    .WithReference(openai);

// AI Backend with Semantic Kernel
builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
```

### Azure AI Foundry

**Hosting Integration:**
```csharp
var foundry = builder.AddAzureAIFoundry("foundry");
foundry.AddModel("gpt-4o");

builder.AddProject<Projects.App>()
    .WithReference(foundry)
    .WithRoleAssignments(foundry, CognitiveServicesUser);
```

**Features:**
- Automatic Bicep generation for Azure deployment
- Role-based access control via `WithRoleAssignments()`
- Health checks and OpenTelemetry tracing built-in
- Support for `IChatClient` interface chaining

---

## 5. Example Projects

### Official Microsoft Samples

| Project | Description | Link |
|---------|-------------|------|
| **aspire-samples** | Official Aspire samples including eShop | github.com/dotnet/aspire-samples |
| **eShopLite** | Reference app with semantic search, RAG, MCP | Azure-Samples/eShopLite-SemanticSearch |
| **Creative Writing Assistant** | Multi-agent app with Semantic Kernel + Aspire | Microsoft Learn Samples |
| **aspire-semantic-kernel-basic-chat-app** | Basic chat with Aspire + Semantic Kernel | GitHub |
| **Generative AI for Beginners (.NET)** | 5-lesson course on AI with .NET | GitHub |

---

## Summary Table: AI Integration Compatibility

| Integration | Hosting Package | Client Package | Health Checks | Tracing |
|-------------|-----------------|----------------|---------------|---------|
| Azure AI Foundry | `Aspire.Hosting.Azure.AIFoundry` | `Aspire.Azure.AI.Inference` | Yes | Yes |
| Azure AI Search | `Aspire.Hosting.Azure.Search` | Built-in | Yes | Yes |
| OpenAI | `Aspire.Hosting.Azure.OpenAI` | Built-in | Yes | Yes |
| Ollama | `CommunityToolkit.Aspire.Hosting.Ollama` | OllamaSharp | Yes | Limited |
| Qdrant | `Aspire.Hosting.Qdrant` | `Qdrant.Client` | Yes | No |
| Milvus | `Aspire.Hosting.Milvus` | `Milvus.Client` | Yes | No |

---

## Sources

- What's new in Aspire 9.0 - Microsoft Learn
- Announcing Aspire 9.5 - .NET Blog
- What's new in Aspire 13 - aspire.dev
- Aspire Azure AI Foundry integration - Microsoft Learn
- Aspire Azure AI Search integration - Microsoft Learn
- Ollama integration - Microsoft Learn
- Using Local AI models with Aspire - .NET Blog
- Build AI Applications with Semantic Kernel and .NET Aspire
- Qdrant/Milvus integration - Microsoft Learn
- Service discovery overview - Microsoft Learn
- Telemetry with Aspire Dashboard - Microsoft Learn
