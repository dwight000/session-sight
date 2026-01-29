# SessionSight Backlog

> **Single source of truth for task tracking.** Update this file every session.

---

## Current Status

**Phase**: Phase 1 complete (core), CI/CD deferred
**Next Action**: P1-013 (CI/CD) or P2-001 (AI extraction)
**Last Updated**: January 28, 2026 (evening)

---

## Active Work

<!-- When you start a task, move it here. Only ONE task at a time. -->

*No task in progress*

---

## Task Table

| ID | Task | Size | Phase | Status | Blocked-By |
|----|------|------|-------|--------|------------|
| **Phase 0: Azure Setup & GitHub** |||||
| P0-000 | Create private GitHub repo (session-sight) | S | 0 | Done | - |
| P0-001 | Create Azure resource group | S | 0 | Done | - |
| P0-002 | Provision Azure SQL (free tier) | S | 0 | Done | P0-001 |
| P0-003 | Provision Azure AI Search (free tier) | S | 0 | Done | P0-001 |
| P0-004 | Set up Azure OpenAI with GPT-4o models | M | 0 | Done | P0-001 |
| P0-005 | Provision Azure AI Document Intelligence | S | 0 | Done | P0-001 |
| P0-006 | Create Azure Key Vault | S | 0 | Done | P0-001 |
| ~~P0-007~~ | ~~Create Azure Container Registry (ACR)~~ | - | - | Removed | *azd handles automatically* |
| P0-008 | Configure budget alert on resource group | S | 0 | Tabled | *global budget alert exists* |
| P0-009 | Configure OpenAI daily spend alert | S | 0 | Tabled | *global budget alert exists* |
| P0-010 | Configure connection strings | S | 0 | Done | - |
| **Spike: Agent Framework** |||||
| B-001 | Agent Framework spike (see pass/fail criteria in PROJECT_PLAN) | XL | Spike | Done | - |
| B-025 | Agent Framework compatibility gate - pin versions, document | M | Spike | Done | B-001 |
| **Phase 1: Foundation** |||||
| P1-001 | Set up .NET 9 solution with Aspire | M | 1 | Done | P0-010 |
| P1-002 | Create domain models (Clinical Schema) | M | 1 | Done | P1-001 |
| P1-003 | Connect to Azure SQL database | M | 1 | Done | P1-001, P0-002 |
| P1-004 | Basic API endpoints (CRUD) | M | 1 | Done | P1-002, P1-003 |
| P1-005 | Azure Blob Storage integration | M | 1 | Done | P1-001 |
| P1-006 | Add .gitignore (standard .NET template) | S | 1 | Done | P1-001 |
| P1-007 | Add .editorconfig (standard .NET formatting) | S | 1 | Done | P1-001 |
| P1-008 | Add LICENSE file (MIT) | S | 1 | Done | P1-001 |
| P1-009 | Add local build instructions to README | S | 1 | Done | P1-001 |
| P1-010 | Create SessionSight.Core.Tests project | S | 1 | Done | P1-001 |
| P1-011 | Create SessionSight.Api.Tests project | S | 1 | Done | P1-004 |
| P1-012 | Set up test coverage reporting | M | 1 | Done | P1-010, P1-011 |
| P1-013 | Set up GitHub Actions ci.yml (build + test on PR) | M | 1 | Ready | P1-001 |
| B-026 | Configure GitHub OIDC auth for Azure | M | 1 | Ready | P0-001 |
| B-027 | Map CI/CD secrets and vars to GitHub environments | M | 1 | Ready | B-026 |
| B-028 | CI quality gates (format, lint, coverage threshold) | M | 1 | Ready | P1-013 |
| B-018 | Wire up 80% coverage enforcement in CI | M | 1 | Ready | P1-012, B-028 |
| P1-014 | Configure branch protection (require PR, passing checks) | S | 1 | Ready | P1-013 |
| P1-015 | Export Bicep via `azd infra synth` and commit to /infra | M | 1 | Ready | P0-010 |
| P1-016 | Add Application Insights (via Aspire) | S | 1 | Done | P1-001 |
| P1-017 | Add Key Vault integration (via Aspire) | M | 1 | Done | P1-001, P0-006 |
| P1-018 | Add health check endpoint (`/health`) | S | 1 | Done | P1-004 |
| P1-019 | Configure OpenAPI/Swagger (auto-generated) | S | 1 | Done | P1-004 |
| P1-020 | Basic error handling patterns (try-catch, logging) | M | 1 | Done | P1-004 |
| P1-021 | Initialize Gitflow branches (main, develop) | S | 1 | Done | P1-001 |
| P1-022 | Make GitHub repo public (verify no secrets in history) | S | 1 | Ready | P1-006, P1-009 |
| **Phase 2: AI Extraction Pipeline** |||||
| P2-001 | Azure OpenAI setup (GPT-4o, GPT-4o-mini, embeddings) | M | 2 | Ready | B-025 |
| P2-002 | Model Router implementation | L | 2 | Blocked | P2-001 |
| P2-003 | Intake Agent | L | 2 | Blocked | P2-002 |
| P2-004 | Clinical Extractor Agent | XL | 2 | Blocked | P2-002 |
| P2-005 | Risk Assessor Agent (safety-critical) | XL | 2 | Blocked | P2-004 |
| P2-006 | Agent-to-tool callbacks | L | 2 | Blocked | P2-003 |
| P2-007 | Confidence scoring | M | 2 | Blocked | P2-004 |
| P2-008 | Blob trigger ingestion (Azure Function) | L | 2 | Blocked | P1-005 |
| B-010 | Exponential backoff for OpenAI/Search | M | 2 | Blocked | P2-001 |
| B-011 | Idempotent job IDs for blob trigger | M | 2 | Blocked | P2-008 |
| B-012 | Dead-letter handling for failed ingestion | M | 2 | Blocked | P2-008 |
| B-013 | Dedupe strategy blob->SQL->AI Search | M | 2 | Blocked | P2-004 |
| B-019 | Telemetry redaction for PHI in traces | M | 2 | Blocked | P1-016 |
| B-032 | Document size validation (reject >30 pages) | M | 2 | Blocked | P2-008 |
| B-033 | Internal service auth (Function->API) | M | 2 | Blocked | P2-008 |
| B-034 | Fix idempotency race condition (SQL MERGE with HOLDLOCK) | M | 2 | Blocked | P2-008 |
| B-035 | Synchronous AI Search indexing | M | 2 | Blocked | P2-004 |
| B-036 | Document Intelligence failure handling | M | 2 | Blocked | P2-008 |
| B-037 | Tool call limit graceful handling | M | 2 | Blocked | P2-006 |
| P2-009 | Create glossary of domain terms | S | 2 | Blocked | P2-004 |
| P2-010 | Create sequence diagrams for agent interactions | M | 2 | Blocked | P2-006 |
| **Phase 3: Summarization & RAG** |||||
| P3-001 | Summarizer Agent (3 levels) | XL | 3 | Blocked | P2-005 |
| P3-002 | Azure AI Search vector index | M | 3 | Blocked | P2-001 |
| P3-003 | Embedding pipeline (text-embedding-3-large) | L | 3 | Blocked | P3-002 |
| P3-004 | Q&A Agent with RAG | XL | 3 | Blocked | P3-003 |
| B-003 | Synthetic data generator script | M | 3 | Blocked | P2-004 |
| B-014 | Reindex/backfill job for AI Search | M | 3 | Blocked | P3-002 |
| **Pre-Phase 3 Checkpoint (Tabled Items)** |||||
| B-020 | RBAC / Entra ID authentication | L | 3+ | Tabled | - |
| B-021 | Audit logging & compliance | L | 3+ | Tabled | - |
| B-022 | OpenAI cost guardrails (full) | M | 3+ | Tabled | - |
| B-023 | Data lifecycle (SQL + Blob) | M | 3+ | Tabled | - |
| B-024 | Private networking baseline | L | 3+ | Tabled | - |
| **Phase 4: Risk Dashboard & UI** |||||
| P4-001 | Supervisor review queue/dashboard | XL | 4 | Blocked | P3-001 |
| P4-002 | Risk trend visualization | L | 4 | Blocked | P4-001 |
| P4-003 | Patient history timeline view | L | 4 | Blocked | P4-001 |
| P4-004 | Flagged session approve/dismiss workflow | M | 4 | Blocked | P4-001 |
| **Phase 5: Polish & Testing** |||||
| P5-001 | Integration tests (golden files) | L | 5 | Blocked | P2-005 |
| P5-002 | Data flow diagrams (document->agent->DB) | M | 5 | Blocked | B-004 |
| P5-003 | API usage examples | S | 5 | Blocked | P1-019 |
| B-004 | Architecture diagrams (Mermaid) | M | 5 | Blocked | P2-010 |
| B-005 | Load testing setup | M | 5 | Blocked | P5-001 |
| B-015 | Contract tests for API DTOs | M | 5 | Blocked | P1-004 |
| B-016 | Load/concurrency tests | M | 5 | Blocked | P5-001 |
| B-017 | Safety/red-team evals | L | 5 | Blocked | P2-005 |
| B-038 | Golden files for non-risk fields | L | 5 | Blocked | P2-004 |
| **Phase 6: Deployment** |||||
| P6-001 | Configure dev environment (development Azure resources) | M | 6 | Blocked | P5-001 |
| P6-002 | Configure prod environment (production Azure resources) | M | 6 | Blocked | P6-001 |
| P6-003 | GitHub Actions deploy.yml (azd deploy) | M | 6 | Blocked | P6-001 |
| B-029 | Infra drift checks: azd infra synth + bicep build | M | 6 | Blocked | P1-015 |
| B-030 | Promotion model: dev->prod approval rules | M | 6 | Blocked | P6-003 |
| B-031 | Rollback strategy: keep last good artifact | M | 6 | Blocked | P6-003 |
| P6-004 | Environment-specific configuration | M | 6 | Blocked | P6-002 |
| P6-005 | Create GitHub Release with SemVer tag (v1.0.0) | S | 6 | Blocked | P6-003 |
| P6-006 | Enable Dependabot for dependency updates | S | 6 | Blocked | P6-005 |
| P6-007 | Demo data and walkthrough | M | 6 | Blocked | P6-002 |

---

## Completed Tasks

| ID | Task | Completed |
|----|------|-----------|
| P0-000 | Create private GitHub repo (session-sight) | 2026-01-25 |
| P0-001 | Create Azure resource group | 2026-01-25 |
| P0-002 | Provision Azure SQL (free tier) | 2026-01-25 |
| P0-003 | Provision Azure AI Search (free tier) | 2026-01-25 |
| P0-004 | Set up Azure OpenAI with GPT-4o models | 2026-01-25 |
| P0-005 | Provision Azure AI Document Intelligence | 2026-01-25 |
| P0-006 | Create Azure Key Vault | 2026-01-25 |
| B-002 | Create 37 golden file test cases for risk assessment | 2026-01-22 |
| B-006 | Update Phase 2 verification checklist with threshold references | 2026-01-24 |
| B-007 | Review agent-tool-callbacks.md spec | 2026-01-24 |
| B-008 | Reconcile risk threshold to 0.9 constant | 2026-01-24 |
| B-009 | Fix vector dims 1536->3072 in Phase 3 spec | 2026-01-24 |
| P0-010 | Configure connection strings (all keys in Key Vault) | 2026-01-26 |
| B-001 | Agent Framework spike — PASS (Option A, Foundry Agents) | 2026-01-26 |
| B-025 | Agent Framework compatibility gate - pin versions | 2026-01-28 |
| P1-001 | Set up .NET 9 solution with Aspire | 2026-01-28 |
| P1-002 | Create domain models (Clinical Schema) | 2026-01-28 |
| P1-003 | Connect to Azure SQL database | 2026-01-28 |
| P1-004 | Basic API endpoints (CRUD) | 2026-01-28 |
| P1-005 | Azure Blob Storage integration | 2026-01-28 |
| P1-006 to P1-012 | Config files, tests, coverage | 2026-01-28 |
| P1-016 to P1-021 | Health, OpenAPI, error handling, branches | 2026-01-28 |
| - | Planning complete | 2026-01-24 |

---

## Session Log (Last 5)

| Date | What Happened |
|------|---------------|
| 2026-01-28 (PM) | **Phase 1 core complete.** Built all 8 projects (Core, Infrastructure, Api, Agents, AppHost, ServiceDefaults, 2 test projects). 75 unit tests passing. Aspire runs locally with SQL Server + Azurite containers. EF migrations applied. 11 API endpoints working. Scalar UI added. Deferred: CI/CD (P1-013-015), make public (P1-022). |
| 2026-01-26 (PM) | **B-001 Agent Framework spike: PASS.** Option A (Foundry Agents) works. Created AI Hub + Project, connected OpenAI resource. Built AgentSpike console app: clinical extraction with tool calling, all 4 verification items pass. Aspire AppHost compiles with OpenAI wiring. Wrote SPIKE-REPORT.md. GO for Phase 1. |
| 2026-01-25 (PM) | **Phase 0 complete.** Created GitHub repo, SSH keys. Provisioned all Azure resources: SQL, AI Search, OpenAI (3 models), Document Intelligence, Key Vault. Stored SQL password in Key Vault. Moved plan docs into repo. |
| 2026-01-25 (AM) | **Documentation reorg.** Created BACKLOG.md, WORKFLOW.md. Refactored PROJECT_PLAN.md to remove duplicate tracking. |
| 2026-01-24 (PM) | **Major gap analysis & fixes.** Deleted 4 obsolete files. Added Azure AI Document Intelligence. Fixed 12 gaps. Added B-032 to B-038. Backlog now 38 items. |
| 2026-01-24 (AM) | Addressed POC feedback: fixed risk threshold (0.9), vector dims (3072), created resilience.md, added SLO table. Added B-020-B-031. |
| 2026-01-22 | Created 37 golden files for risk assessment (B-002). Used 3-agent review process. Fixed schema issues, added edge cases. |

---

## Size Legend

| Size | Effort |
|------|--------|
| S | < 1 hour |
| M | 1-4 hours |
| L | 1 day |
| XL | Multi-day |

## Status Legend

| Status | Meaning |
|--------|---------|
| Ready | Can be started now |
| In-Progress | Currently being worked on |
| Blocked | Waiting on dependencies |
| Done | Completed |
| Tabled | Deferred, revisit later |
