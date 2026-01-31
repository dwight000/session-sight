# SessionSight - Portfolio Project Plan

> **Purpose**: This document is a comprehensive project plan designed to be self-contained. A new Claude session can read this document and immediately understand the full context, decisions made, and how to proceed with implementation.

---

## Background & Motivation

**Why this project exists**:
- Public GitHub portfolio demonstrating job-ready skills
- Showcase Azure cloud architecture (OpenAI, AI Search, SQL, Blob Storage)
- Demonstrate AI/ML expertise with multi-agent orchestration
- Highlight .NET 9 and Aspire for enterprise development
- AI-assisted development: this project itself is built with Claude Code

---

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Project Name** | SessionSight | Catchy, product-like branding |
| **Development Approach** | Cloud-backed | Azure OpenAI + AI Search even for local dev |
| **LLM Provider** | Azure OpenAI | Simpler, guaranteed availability, single provider |
| **LLM Strategy** | Model Routing | GPT-4o for extraction/risk, GPT-4o-mini for intake/simple |
| **Embeddings** | text-embedding-3-large | Azure OpenAI native, for RAG |
| **Frontend Priority** | Minimal API-first | Focus on backend/agents, add UI later |
| **MCP** | Skip | Not needed for fixed pipeline |
| **Document Parsing** | Azure AI Document Intelligence | OCR + section identification; recommended by Microsoft for RAG pipelines |
| **Testing** | Integration + golden files | AI outputs non-deterministic, verify structure not exact values |
| **CI/CD** | GitHub Actions | GitHub portfolio, Microsoft pushing GH Actions for new features |
| **Secrets** | Azure Key Vault | Best practice; dotnet user-secrets for local dev |
| **Environments** | Local + Dev (cloud) | Prod can be added later if needed |
| **Observability** | Application Insights + Aspire Dashboard | Built-in Aspire support |
| **API Auth** | API key | Simple for POC |
| **API Docs** | Auto-generated OpenAPI | Built-in .NET 9 support |
| **Container Registry** | Azure Container Registry (ACR) | Added via Bicep when needed |
| **Branch Strategy** | Gitflow | main/develop/feature branches |
| **Versioning** | SemVer + Git tags | Standard (1.0.0, 1.1.0, etc.) |
| **Code Coverage** | 80% target | Enforced in CI |
| **Code Formatting** | .editorconfig | Standard .NET rules |
| **IaC** | Hand-written Bicep | Full control, modular design, committed to repo |

See `plan/docs/decisions/` for full Architecture Decision Records.

---

## Project Overview

**Project Name**: SessionSight

**Concept**: An AI-powered platform that helps mental health practices by:
1. Ingesting therapist session notes (text, PDFs)
2. Extracting structured data using a Clinical Schema (80+ fields)
3. Generating multi-level summaries (session -> patient -> practice)
4. Enabling RAG-powered Q&A about patient histories
5. Flagging at-risk patients for supervisor review

### User Personas

| Persona | Role | Primary Actions | Key Needs |
|---------|------|-----------------|-----------|
| **Sarah (Therapist)** | Licensed therapist, 20 patients/week | Uploads notes after sessions, reviews extractions | Quick upload, minimal friction, accurate extraction |
| **Dr. Chen (Supervisor)** | Clinical supervisor, oversees 5 therapists | Reviews flagged sessions, monitors patient trends | Risk alerts, patient history summaries, trend dashboards |
| **Mike (Admin)** | Practice administrator | Bulk uploads, system configuration, reports | Batch processing, usage metrics, data exports |

---

## Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Orchestration** | .NET Aspire 9.x | Cloud-native service orchestration |
| **Backend API** | .NET 9, ASP.NET Core | REST API |
| **AI Framework** | Microsoft Agent Framework | Multi-agent orchestration |
| **Document Processing** | Azure AI Document Intelligence | OCR, PDF parsing, section identification |
| **LLM** | Azure OpenAI (GPT-4o) | Extraction, summarization, Q&A |
| **Embeddings** | Azure OpenAI (text-embedding-3-large) | Vector embeddings for RAG |
| **Model Routing** | Custom | GPT-4o / GPT-4o-mini selection by task |
| **Vector DB** | Azure AI Search | RAG hybrid search |
| **Database** | Azure SQL | Structured data (free tier) |
| **Storage** | Azure Blob Storage | Document storage (local emulator) |
| **Deployment** | Aspire local + GitHub Actions | Bicep IaC, CI/CD workflows |

> **Note (Jan 2026):** Microsoft Agent Framework is in **public preview**. GA expected later 2026. Package names may change (`Microsoft.Agents.*` vs `Azure.AI.Agents`). See B-001 spike and B-025 compatibility gate before Phase 2. Delivered via Azure AI Foundry "Agent Service."

### Environment Model

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SESSIONSIGHT ENVIRONMENTS                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  LOCAL                              DEV (Cloud)                             │
│  ─────                              ───────────                             │
│  Your machine                       Azure Container Apps                    │
│  dotnet run / Aspire                Deployed via GitHub Actions             │
│  For coding, debugging              For testing, demos, portfolio showcase  │
│                                                                             │
│  ┌─────────────────────┐            ┌─────────────────────┐                │
│  │ API runs locally    │            │ API runs in Azure   │                │
│  │ Azurite (blob emu)  │            │ Azure Blob Storage  │                │
│  └─────────┬───────────┘            └─────────┬───────────┘                │
│            │                                  │                             │
│            └──────────────┬───────────────────┘                             │
│                           ▼                                                 │
│            ┌─────────────────────────────────────┐                         │
│            │  SHARED AZURE SERVICES               │                         │
│            │  (rg-sessionsight-dev)               │                         │
│            │  - Azure OpenAI                      │                         │
│            │  - Azure SQL                         │                         │
│            │  - Azure AI Search                   │                         │
│            │  - Document Intelligence             │                         │
│            │  - Key Vault                         │                         │
│            └─────────────────────────────────────┘                         │
│                                                                             │
│  PROD (Future)                                                              │
│  ─────────────                                                              │
│  Not implemented. Can be added later if needed for:                         │
│  - Separate resources for production workloads                              │
│  - Approval gates before deployment                                         │
│  - Different scaling/pricing tiers                                          │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Azure subscription required** for development (OpenAI, AI Search, SQL).

---

## Multi-Agent Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    ORCHESTRATOR AGENT                        │
│              (Microsoft Agent Framework)                     │
└─────────────────────┬───────────────────────────────────────┘
                      │
        ┌─────────────┼─────────────┬─────────────┐
        ▼             ▼             ▼             ▼
┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐
│  INTAKE   │  │ CLINICAL  │  │   RISK    │  │SUMMARIZER │
│   AGENT   │  │ EXTRACTOR │  │ ASSESSOR  │  │   AGENT   │
│ (4o-mini) │  │  (GPT-4o) │  │  (GPT-4o) │  │  (GPT-4o) │
└───────────┘  └───────────┘  └───────────┘  └───────────┘
                      │
                      ▼
              ┌───────────────┐
              │   Q&A AGENT   │
              │(4o-mini/GPT-4o)│
              └───────────────┘
```

### Model Routing Strategy

| Task | Model | Rationale |
|------|-------|-----------|
| Document Intake | GPT-4o-mini | Simple parsing, high volume, cost-effective |
| Clinical Extraction | GPT-4o | Highest accuracy needed |
| Risk Assessment | GPT-4o | Safety-critical |
| Summarization | GPT-4o | Quality summaries |
| Simple Q&A | GPT-4o-mini | Fast responses, lower cost |
| Complex Q&A | GPT-4o | Multi-document reasoning |
| Embeddings | text-embedding-3-large | Vector search for RAG |

---

## Clinical Schema

See: `plan/docs/specs/clinical-schema.md` for full 80+ field schema.

**Key categories:**
- Session Info (10 fields)
- Presenting Concerns (7 fields)
- Mood Assessment (7 fields)
- Risk Assessment (12 fields) - **Safety Critical**
- Mental Status Exam (9 fields)
- Interventions (9 fields)
- Diagnoses (6 fields)
- Treatment Progress (7 fields)
- Next Steps (8 fields)
- Extraction Metadata (8 fields)

**Confidence Thresholds:**
- Risk Assessment: 0.9 minimum (flags for human review if below) - see ADR-004
- Session Info: 0.7 minimum
- Others: 0.6 minimum

---

## Implementation Phases

> **Execution Order:** Phase 0 → B-001/B-025 spike (blocks Phase 2) → Phase 1 → Phase 2 → Revisit tabled items (B-020-024) → Phase 3 → Phase 4 → Phase 5 → Phase 6

> **Task Tracking:** Individual tasks for each phase are tracked in `plan/docs/BACKLOG.md`

### Phase 0: Azure & GitHub Setup

Create private GitHub repository (will be made public after Phase 1 when secrets/configs are properly secured). Provision all required Azure resources: resource group, Azure SQL (free tier), Azure AI Search (free tier), Azure OpenAI with GPT-4o models, Azure AI Document Intelligence for PDF/OCR, Key Vault for secrets, and Container Registry. Set up cost guardrails with budget alerts on the resource group and OpenAI daily spend alerts. Configure connection strings for all services.

**Spec:** `plan/docs/specs/azure-setup.md`

### Phase 1: Foundation

Build the .NET 9 solution structure with Aspire orchestration. Create domain models implementing the Clinical Schema, connect to Azure SQL, and build basic CRUD API endpoints. Integrate Azure Blob Storage for document uploads.

Set up project infrastructure: .gitignore, .editorconfig, LICENSE (MIT), and README build instructions. Create test projects (Core.Tests, Api.Tests) with coverage reporting.

Configure CI/CD with GitHub Actions for build/test on PR, GitHub OIDC auth for Azure, environment secrets, and quality gates (format, lint, coverage). Set up branch protection and write hand-written Bicep IaC with infra deployment workflow.

Add observability with Application Insights, Key Vault integration, health check endpoint, and auto-generated OpenAPI docs. Establish error handling patterns and initialize Gitflow branches.

**Spec:** `plan/docs/specs/phase-1-foundation.md`

### Phase 2: AI Extraction Pipeline

**Blocked by:** B-001/B-025 Agent Framework spike must pass first.

Implement the core AI pipeline: Azure OpenAI integration (GPT-4o, GPT-4o-mini, embeddings), Model Router for cost-optimized model selection, and the agent pipeline (Intake Agent → Clinical Extractor Agent → Risk Assessor Agent). Build agent-to-tool callbacks for multi-step reasoning and confidence scoring for all extractions.

Create blob trigger ingestion via Azure Function. Implement resilience patterns: exponential backoff, idempotent job IDs, dead-letter handling, and dedupe strategy across blob/SQL/AI Search.

Document domain terms in a glossary and create sequence diagrams for agent interactions.

**Specs:** `phase-2-ai-extraction.md`, `blob-trigger-ingestion.md`, `agent-tool-callbacks.md`, `resilience.md`

### Pre-Phase 3 Checkpoint

Before starting Phase 3, revisit tabled items and decide which to implement:
- B-020: RBAC / Entra ID (recommended if demoing to employers)
- B-021: Audit logging
- B-022: OpenAI cost guardrails (full implementation)
- B-023: Data lifecycle (SQL/Blob retention)
- B-024: Private networking

### Phase 3: Summarization & RAG

Build the Summarizer Agent with 3 summary levels (session, patient, practice). Set up Azure AI Search vector index and embedding pipeline using text-embedding-3-large. Implement the Q&A Agent with RAG-powered retrieval. Add reindex/backfill job for AI Search maintenance.

Create synthetic therapy notes generator script for diverse test scenarios covering various conditions and risk levels.

**Spec:** `plan/docs/specs/phase-3-summarization-rag.md`

### Phase 4: Risk Dashboard & UI

Build the supervisor-facing UI for reviewing flagged sessions and monitoring patient risk. Implement supervisor review queue/dashboard, risk trend visualization, patient history timeline, and approve/dismiss workflow for flagged extractions.

This is a minimal frontend (API-first approach) - focus on functionality over polish.

**Spec:** `plan/docs/specs/phase-4-risk-dashboard.md`

### Phase 5: Polish & Testing

Harden the system with comprehensive testing and documentation. Implement integration tests with golden files, contract tests for API DTOs, load/concurrency tests, and safety/red-team evaluations.

Create documentation: architecture diagrams (Mermaid), data flow diagrams (document → agent → DB), and API usage examples. Expand golden files to cover all 82 schema fields.

**Spec:** `plan/docs/specs/phase-5-polish-testing.md`

### Phase 6: Deployment

Configure dev and prod environments with appropriate Azure resources. Set up GitHub Actions deploy.yml for application deployment. Implement infra drift checks (Bicep what-if), dev→prod promotion model with approval rules, and rollback strategy.

Create GitHub Release with SemVer tag (v1.0.0), enable Dependabot for dependency updates, and prepare demo data with walkthrough documentation.

**Spec:** `plan/docs/specs/phase-6-deployment.md`

---

## Repository Structure

```
session-sight/
├── .github/
│   └── workflows/                # GitHub Actions CI/CD
│       ├── ci.yml                # Build + test on PR
│       └── deploy.yml            # Deploy to Azure (on release)
├── plan/docs/
│   ├── PROJECT_PLAN.md           # Stable context (what/why) - rarely changes
│   ├── BACKLOG.md                # Task tracker (what's next) - updated every session
│   ├── WORKFLOW.md               # Claude session instructions (how)
│   ├── glossary.md               # (planned) Domain terms and definitions
│   ├── specs/                    # Implementation specs
│   │   ├── azure-setup.md        # Phase 0: Azure resource provisioning
│   │   ├── clinical-schema.md
│   │   ├── phase-1-foundation.md
│   │   ├── phase-2-ai-extraction.md
│   │   ├── phase-3-summarization-rag.md
│   │   ├── phase-4-risk-dashboard.md
│   │   ├── phase-5-polish-testing.md
│   │   ├── phase-6-deployment.md
│   │   ├── agent-tool-callbacks.md
│   │   ├── blob-trigger-ingestion.md
│   │   └── resilience.md
│   ├── diagrams/                 # (planned) Architecture & flow diagrams
│   ├── research/                 # Technology research
│   └── decisions/                # Architecture Decision Records
│       ├── ADR-002-error-handling.md
│       └── ADR-004-risk-validation.md
├── infra/                        # Hand-written Bicep IaC
│   ├── main.bicep                # Entry point (subscription scope)
│   ├── main.parameters.dev.json  # Dev environment values
│   ├── main.parameters.prod.json # Prod environment values
│   └── modules/                  # Modular resource definitions
├── src/
│   ├── SessionSight.AppHost/      # Aspire orchestration
│   ├── SessionSight.Api/          # REST API
│   ├── SessionSight.Agents/       # Agent implementations
│   ├── SessionSight.BlobTrigger/  # Azure Function for blob ingestion
│   ├── SessionSight.Core/         # Domain models, schema
│   ├── SessionSight.Infrastructure/  # Data access
│   └── SessionSight.ServiceDefaults/ # Aspire defaults
├── tests/
│   ├── SessionSight.Core.Tests/
│   └── SessionSight.Api.Tests/
├── data/synthetic/               # Generated test data
│   └── golden-files/risk-assessment/  # 37 risk assessment test cases (B-002)
├── .editorconfig                 # Code formatting rules
├── .gitignore                    # Git ignore patterns
├── LICENSE                       # MIT license
└── README.md
```

---

## Research Completed

Research docs in `plan/docs/research/`:

1. **aspire-ai-capabilities-research-2026.md**: .NET Aspire 9.x with GenAI Telemetry Visualizer, Azure OpenAI component, Azure AI Search component.

2. **azure-ai-foundry-agent-research-jan2026.md**: Microsoft Agent Framework (preview), Azure AI Foundry integration patterns.

**Decisions made**: Azure OpenAI (not Claude), skip MCP - rationale captured in "Decisions Made" table above.

---

## Service Level Objectives (SLOs)

POC targets for phase exit gates:

| Metric | Target | Phase | Measurement |
|--------|--------|-------|-------------|
| Build/test pass | 100% | All | CI pipeline |
| Code coverage | 80% | All | CI coverage report |
| Extraction P95 latency | <30s | Phase 2 | Application Insights |
| Risk field F1 score | >0.90 | Phase 2 | Golden file tests |
| Overall extraction F1 | >0.85 | Phase 2 | Synthetic note validation |
| Cost per note | <$0.50 | Phase 2 | Azure cost analysis |
| RAG precision@5 | >0.80 | Phase 3 | Eval harness |
| Q&A relevance | >0.75 | Phase 3 | Human eval (20 queries) |
| API availability | 99% (dev) | Phase 6 | Uptime monitoring |

See individual phase specs for detailed exit criteria.

---

## Success Criteria

Portfolio demonstrates:
- [ ] Multi-agent AI orchestration (Microsoft Agent Framework)
- [ ] Structured data extraction with 80+ field schema
- [ ] Confidence scoring and source mapping
- [ ] Multi-level summarization
- [ ] RAG with Azure AI Search + embeddings
- [ ] Model routing (GPT-4o / GPT-4o-mini)
- [ ] .NET Aspire orchestration
- [ ] Azure OpenAI integration
- [ ] Agent-to-tool callbacks (agentic reasoning)
- [ ] Blob trigger ingestion (Azure Functions)
- [ ] Integration testing with golden files
- [ ] Clean architecture patterns
- [ ] **Modern DevOps Practices:**
  - [ ] GitHub Actions CI/CD
  - [ ] Infrastructure as Code (hand-written Bicep)
  - [ ] Azure Key Vault for secrets
  - [ ] Application Insights observability
  - [ ] 80% code coverage
  - [ ] OpenAPI documentation

---

## How to Resume This Project

If you are a new Claude session:

1. **Read `plan/docs/BACKLOG.md`** - Current status, active work, and task list
2. **Follow `plan/docs/WORKFLOW.md`** - Session start/end instructions and task selection
3. **Read this document** - For context, decisions, and architecture
4. **Check phase specs** - `plan/docs/specs/phase-*.md` for implementation details
5. **Check research** - `plan/docs/research/` for technology deep-dives

### Key Constraints
- Must use Microsoft Agent Framework (not raw Semantic Kernel)
- Domain is mental health/therapy notes
- Cloud-backed dev: Azure SQL, Azure OpenAI, AI Search (all Azure)
- Model routing: GPT-4o for extraction/risk/summaries, GPT-4o-mini for intake/simple Q&A
- API-first: minimal frontend, focus on backend/agents
- Testing: Integration tests + golden files (AI outputs are non-deterministic)
- CI/CD: GitHub Actions (not Azure DevOps)
- Branching: Gitflow (main/develop/feature)
- Secrets: Azure Key Vault (never commit secrets)
- IaC: Hand-written Bicep committed to repo, deployed via GitHub Actions

### Security Disclaimer

> **This is a portfolio POC with synthetic data only. NOT production-ready for real PHI.**

| Implemented | Not Implemented (Out of Scope) |
|-------------|-------------------------------|
| Azure Key Vault for secrets | HIPAA compliance / BAA |
| `dotnet user-secrets` locally | Comprehensive audit logging |
| HTTPS transport encryption | RBAC / Entra ID |
| Azure SQL TDE (default) | Customer-managed encryption keys |
| API key authentication | Penetration testing |
| No PII in logs (redacted) | Incident response procedures |

All test data is AI-generated with fictional patients. Never use real patient data.

---

## Session Workflow

See `plan/docs/WORKFLOW.md` for detailed Claude session instructions.

**Quick Reference:**
- **Start:** Read `BACKLOG.md` → Check Active Work → Pick next Ready task
- **End:** Update task status → Add Session Log entry → Update Last Updated

---

## Task Tracking

**All tasks are tracked in `plan/docs/BACKLOG.md`** - the single source of truth for:
- Current status and next action
- Active work in progress
- Full task table with dependencies
- Session log
- Completed tasks

---

### B-001 Spike: Pass/Fail Criteria

**Objective:** Validate Microsoft Agent Framework is usable for SessionSight before Phase 2 begins.

**Timeline:** 2 days maximum (post-Phase 0)

#### Architecture Decision Required

The spike must first determine which SDK approach to use:

| Option | SDK | Requires | Pros | Cons |
|--------|-----|----------|------|------|
| **A: Foundry Agents** | `Azure.AI.Projects` + `Azure.AI.Agents.Persistent` | Azure AI Foundry Project | Full agent framework, managed state | Requires Foundry project setup |
| **B: Direct OpenAI** | `Azure.AI.OpenAI` | Azure OpenAI resource only | Simpler setup, works with Phase 0 | Manual state management |

> **Note:** Phase 0 provisions standalone Azure OpenAI. If Option A is chosen, add task to create Foundry project.

**Pass Criteria (ALL must pass):**
1. Identify correct NuGet package(s) and pin specific versions
2. Build minimal console app that:
   - Creates an agent with Azure OpenAI backend
   - Agent receives a therapy note excerpt
   - Agent calls a tool (`validate_schema`) - see tool example below
   - Agent returns structured JSON output
3. Aspire integration compiles (add to AppHost)
4. Document exact package versions that work

**Tool Calling Example (must demonstrate):**

```csharp
// Define a tool the agent can call
var validateSchemaTool = new FunctionToolDefinition(
    name: "validate_schema",
    description: "Validates extracted fields against clinical schema",
    parameters: BinaryData.FromObjectAsJson(new {
        type = "object",
        properties = new {
            extraction = new { type = "object", description = "The extracted clinical data" }
        },
        required = new[] { "extraction" }
    })
);

// Agent should be able to:
// 1. Receive therapy note
// 2. Decide to call validate_schema tool
// 3. Process tool result
// 4. Return final structured output
```

**Fail Criteria (ANY triggers fail):**
- Cannot identify working package combination
- Tool calling doesn't work as documented
- Requires workarounds that aren't documented by Microsoft
- API is so unstable that code breaks within 1 week

**If Spike Fails:**
No fallback defined. Reassess project scope and timeline. Consider:
- Delay until GA (Q1-Q2 2026)
- Pivot to different portfolio project
- Use raw `Azure.AI.OpenAI` SDK (less impressive but works)

#### Spike Report Template

```markdown
# B-001 Spike Report: Microsoft Agent Framework

**Date:** [date]
**Duration:** [hours spent]
**Result:** PASS / FAIL

## 1. SDK Choice
- **Selected:** Option A (Foundry) / Option B (Direct OpenAI)
- **Rationale:** [why this choice]

## 2. Packages Tested
| Package | Version | Status |
|---------|---------|--------|
| Azure.AI.OpenAI | x.x.x | Works/Fails |
| ... | ... | ... |

## 3. Working Code Sample
[Include complete minimal example that passes all criteria]

## 4. Tool Calling Verification
- [ ] Tool definition works
- [ ] Agent calls tool autonomously
- [ ] Tool result processed correctly
- [ ] Final output is structured JSON

## 5. Aspire Integration
- [ ] Compiles in AppHost
- [ ] Service discovery works

## 6. Issues Encountered
[List any workarounds needed]

## 7. Recommendation
GO / NO-GO for Phase 2

[If NO-GO, explain blockers and alternatives]
```
