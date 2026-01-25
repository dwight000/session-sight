# SessionSight

> AI-powered clinical notes analysis for mental health practices

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Aspire](https://img.shields.io/badge/Aspire-13.x-6C3483)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![Azure OpenAI](https://img.shields.io/badge/Azure_OpenAI-GPT--4o-0078D4)](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Overview

SessionSight is a portfolio project demonstrating enterprise AI architecture for clinical document processing. It showcases:

- **Multi-agent AI orchestration** using Microsoft Agent Framework
- **Structured data extraction** with an 82-field Clinical Schema
- **RAG-powered Q&A** using Azure AI Search + Azure OpenAI embeddings
- **Model routing** between GPT-4o / GPT-4o-mini for cost optimization
- **.NET Aspire** for cloud-native orchestration

### Why This Exists

This project parallels enterprise AI systems like document summarization pipelines, demonstrating:
- Feature dictionary / schema-based extraction
- Multi-stage processing pipelines
- Confidence scoring and human review flagging
- Production-grade .NET architecture

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET ASPIRE APPHOST                       │
└─────────────────────┬───────────────────────────────────────┘
                      │
        ┌─────────────┼─────────────┬─────────────┐
        ▼             ▼             ▼             ▼
┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐
│SessionSight│ │  Agent    │  │ Azure SQL │  │  Azure    │
│    API    │  │  Service  │  │(free tier)│  │ AI Search │
└───────────┘  └─────┬─────┘  └───────────┘  └───────────┘
                     │
         ┌───────────┼───────────┐
         ▼           ▼           ▼
    ┌─────────┐ ┌─────────┐ ┌─────────┐
    │ Intake  │ │Extract  │ │Summarize│
    │ Agent   │ │ Agent   │ │ Agent   │
    └─────────┘ └─────────┘ └─────────┘
                     │
                     ▼
              ┌─────────────┐
              │ Azure OpenAI│
              │   GPT-4o    │
              └─────────────┘
```

## Technology Stack

| Layer | Technology |
|-------|------------|
| Orchestration | .NET Aspire 13.x |
| Backend | .NET 9, ASP.NET Core |
| AI Framework | Microsoft Agent Framework |
| Document Processing | Azure AI Document Intelligence (OCR, PDF parsing) |
| LLM | Azure OpenAI (GPT-4o, GPT-4o-mini) |
| Embeddings | Azure OpenAI (text-embedding-3-large) |
| Vector Search | Azure AI Search |
| Database | Azure SQL (free tier) |
| Deployment | Aspire + azd (auto-generated Bicep) |

## Features

### 1. Document Ingestion
Upload therapy session notes (text/PDF) for processing.

### 2. Clinical Data Extraction
Extract 80+ structured fields using a defined Clinical Schema:
- Session metadata
- Presenting concerns
- Mood assessment
- **Risk assessment** (safety-critical)
- Mental status exam
- Interventions used
- Diagnoses
- Treatment progress
- Next steps

Each field includes confidence scores and source text mapping.

### 3. Multi-Level Summaries
- **Session**: One-line summary with key points
- **Patient**: Progress narrative across sessions
- **Practice**: Supervisor dashboard metrics

### 4. RAG-Powered Q&A
Ask natural language questions about patient histories:
- "What interventions have worked best for this patient?"
- "Show me all patients with declining mood"
- "Which patients mentioned sleep issues?"

### 5. Risk Flagging
Automatic flagging for supervisor review when:
- Safety concerns detected (SI/SH indicators)
- Low confidence on critical fields
- Treatment regression patterns

### 6. Agent-to-Tool Callbacks (Key Demo)
Agents dynamically call tools, reason about results, and iterate:

```
Orchestrator Agent
    |
    +-> ExtractorAgent.Extract()
            |
            +-> Tool: validate_schema() -> returns validation errors
            +-> Tool: query_patient_history() -> returns prior sessions
            |
            Agent reasons: "Previous sessions show anxiety focus,
                           current note mentions panic - adjust extraction"
            |
            +-> Tool: refine_extraction() -> final result
```

This demonstrates **multi-step reasoning with tool use** - the hallmark of production AI systems.

## Project Structure

```
session-sight/
├── docs/
│   ├── PROJECT_PLAN.md          # Stable context (what/why) - rarely changes
│   ├── BACKLOG.md               # Task tracker (what's next) - updated every session
│   ├── WORKFLOW.md              # Claude session instructions (how)
│   ├── specs/                   # Phase implementation specs
│   │   ├── clinical-schema.md   # 82-field schema definition
│   │   ├── azure-setup.md
│   │   ├── phase-1-foundation.md
│   │   ├── phase-2-ai-extraction.md
│   │   ├── phase-3-summarization-rag.md
│   │   ├── phase-4-risk-dashboard.md
│   │   ├── phase-5-polish-testing.md
│   │   ├── phase-6-deployment.md
│   │   ├── agent-tool-callbacks.md
│   │   ├── blob-trigger-ingestion.md
│   │   └── resilience.md
│   ├── research/                # Technology research
│   └── decisions/               # Architecture Decision Records
├── src/
│   ├── SessionSight.AppHost/     # Aspire orchestration
│   ├── SessionSight.Api/         # REST API
│   ├── SessionSight.Agents/      # AI agent implementations
│   ├── SessionSight.BlobTrigger/ # Azure Function for blob ingestion
│   ├── SessionSight.Core/        # Domain models, schema
│   ├── SessionSight.Infrastructure/  # Data access, storage
│   └── SessionSight.ServiceDefaults/ # Aspire defaults
├── tests/
├── data/synthetic/              # Demo data
└── deploy/
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Docker Desktop (for Azurite blob emulator)
- Azure subscription (AI Foundry, AI Search, Azure SQL)
- Azure CLI (authenticated)

### Local Development

```bash
# Clone repository
git clone https://github.com/[username]/session-sight.git
cd session-sight

# Restore and build
dotnet restore
dotnet build

# Run with Aspire
dotnet run --project src/SessionSight.AppHost
```

The Aspire dashboard will open, showing all services.

### Configuration

```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "AzureAIFoundry": "your-foundry-endpoint",
    "AzureAISearch": "your-search-endpoint"
  }
}
```

## Implementation Phases

| Phase | Status | Description |
|-------|--------|-------------|
| 0. Azure Setup | Planned | Provision Azure resources, configure connections |
| 1. Foundation | Planned | Solution structure, domain models, basic API |
| 2. AI Extraction | Planned | Intake + Extractor + Risk agents, model routing |
| 3. Summarization & RAG | Planned | Multi-level summaries, Q&A agent |
| 4. Risk Dashboard & UI | Planned | Supervisor review queue, risk visualization |
| 5. Polish & Testing | Planned | Integration tests, golden files, documentation |
| 6. Deployment | Planned | CI/CD, environments, release management |

## Key Design Decisions

See [docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md) for comprehensive documentation.

Architecture Decision Records in [docs/decisions/](docs/decisions/):
- **ADR-002**: Error handling patterns
- **ADR-004**: Risk assessment validation strategy

## Demo

*Coming soon: Links to deployed demo and walkthrough video*

## License

MIT License - See [LICENSE](LICENSE)

## Acknowledgments

- Built with [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- Powered by [Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
- Inspired by enterprise document AI systems

---

**Note**: This is a portfolio demonstration project. All patient data is synthetic. Not intended for clinical use.
