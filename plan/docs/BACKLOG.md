# SessionSight Backlog

> **Single source of truth for task tracking.** Update this file every session.

---

## Current Status

**Phase**: Phase 4 (Risk Dashboard & UI) - IN PROGRESS
**Next Action**: P4-002/P4-003 (risk trend visualization / patient history timeline)
**Last Updated**: February 7, 2026

**Milestone**: P4-001 complete — React supervisor dashboard with Vitest + Playwright test coverage

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
| ~~P0-007~~ | ~~Create Azure Container Registry (ACR)~~ | - | - | Removed | *add to Bicep when needed* |
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
| P1-013 | Set up GitHub Actions ci.yml (build + test on PR) | M | 1 | Done | P1-001 |
| B-026 | Configure GitHub OIDC auth for Azure | M | 1 | Done | P0-001 |
| B-027 | Map CI/CD secrets and vars to GitHub environments | M | 1 | Done | B-026 |
| B-028 | CI quality gates (format, lint, coverage threshold) | M | 1 | Done | P1-013 |
| B-018 | Wire up 30% coverage enforcement in CI (raise to 80% by Phase 3) | M | 1 | Done | P1-012, B-028 |
| P1-014 | Configure branch protection (require PR, passing checks) | S | 1 | Done | P1-013 |
| P1-015 | Write Bicep IaC from scratch and commit to /infra | M | 1 | Done | P0-010 |
| P1-016 | Add Application Insights (via Aspire) | S | 1 | Done | P1-001 |
| P1-017 | Add Key Vault integration (via Aspire) | M | 1 | Done | P1-001, P0-006 |
| P1-018 | Add health check endpoint (`/health`) | S | 1 | Done | P1-004 |
| P1-019 | Configure OpenAPI/Swagger (auto-generated) | S | 1 | Done | P1-004 |
| P1-020 | Basic error handling patterns (try-catch, logging) | M | 1 | Done | P1-004 |
| P1-021 | Initialize Gitflow branches (main, develop) | S | 1 | Done | P1-001 |
| P1-022 | Make GitHub repo public (verify no secrets in history) | S | 1 | Done | P1-006, P1-009 |
| B-039 | Basic CRUD integration tests (Patient, Session) | S | 1 | Done | P1-004 |
| **Phase 2: AI Extraction Pipeline** |||||
| P2-001 | Azure OpenAI setup (GPT-4o, GPT-4o-mini, embeddings) | M | 2 | Done | B-025 |
| P2-002 | Model Router implementation | L | 2 | Done | P2-001 |
| P2-003 | Intake Agent | L | 2 | Done | P2-002 |
| P2-004 | Clinical Extractor Agent | XL | 2 | Done | P2-002 |
| P2-005 | Risk Assessor Agent (safety-critical) | XL | 2 | Done | P2-004 |
| P2-006a | Agent tools: Core infra + check_risk_keywords + validate_schema | M | 2 | Done | P2-003 |
| P2-006b | Agent tools: ClinicalExtractor transformation + remaining tools | L | 2 | Done | P2-006a |
| P2-007 | Confidence scoring | M | 2 | Done | P2-004 |
| P2-008 | Blob trigger + ExtractionOrchestrator + Doc Intelligence | XL | 2 | Done | P2-004 |
| B-010 | Exponential backoff for Azure SDK clients (OpenAI/Search/DocIntel) | M | 2 | Done | P2-001 |
| B-011 | Idempotent job IDs for blob trigger | M | 2 | Ready | P2-008 |
| B-012 | Dead-letter handling for failed ingestion | M | 2 | Ready | P2-008 |
| B-013 | Dedupe strategy blob->SQL->AI Search | M | 2 | Ready | P2-004 |
| B-019 | Telemetry redaction for PHI in traces | M | 2 | Ready | P1-016 |
| B-032 | Document size validation (reject >30 pages) | M | 2 | Ready | P2-008 |
| B-033 | Internal service auth (Function->API) | M | 2 | Ready | P2-008 |
| B-034 | Fix idempotency race condition (SQL MERGE with HOLDLOCK) | M | 2 | Ready | P2-008 |
| B-035 | Synchronous AI Search indexing | M | 2 | Ready | P2-004 |
| B-036 | Document Intelligence failure handling | M | 2 | Ready | P2-008 |
| B-048 | Circuit breaker for Azure SDK clients (Polly or custom HttpPipelinePolicy) | M | 2 | Ready | B-010 |
| B-049 | ~~Extract shared LlmResponseParser from duplicated JSON parsing in 3 agents~~ (superseded by B-056) | M | 2 | Done | P2-004 |
| B-050 | Fix fire-and-forget scoped service lifetime in IngestionController | S | 2 | Done | P2-008 |
| B-051 | Add patient-scoping guard to Q&A tools (cross-patient data access) | S | 2 | Done | P3-005 |
| B-052 | Fix OData filter injection in SearchIndexService | S | 2 | Done | P3-002 |
| B-053 | Fail extraction pipeline on JSON parse failure (safety false-negative) | S | 2 | Done | P2-004 |
| B-054 | Add wall-clock timeout to agent loop (5 min) | S | 2 | Done | B-037 |
| B-055 | Fix E2E extraction JSON parse failures (resilient deserialization + prompt fix) | M | 2 | Done | B-053 |
| B-056 | Harden LLM JSON parsing and error handling across all agents | M | 2 | Done | B-055 |
| B-057 | Add response_format json_object + harden E2E field assertions | M | 2 | Done | B-056 |
| B-058 | Full 74-field assertion coverage + 4 string→enum conversions | M | 2 | Done | B-057 |
| B-041 | Bicep: Add Cognitive Services User role to Doc Intel + OpenAI | M | 2 | Done | P2-008 |
| B-042 | Fix AI Foundry → OpenAI: call Azure OpenAI directly (SDK workaround) | M | 2 | Done | B-041 |
| B-043 | Document local dev setup (docs/LOCAL_DEV.md) | M | 2 | Done | - |
| B-044 | Fix SessionRepository.UpdateAsync concurrency bug in extraction | M | 2 | Done | B-042 |
| B-045 | Create deterministic E2E test runner script | S | 1 | Done | - |
| B-046 | Add file logging for local dev (Serilog) | S | 1 | Ready | - |
| B-047 | Replace Aspire with Docker Compose ([plan](../../.claude/plans/replace_aspire_docker_compose_draft.md)) | M | 1 | Tabled | B-046 |
| B-037 | Tool call limit graceful handling | M | 2 | Done | P2-006b |
| B-040 | Stub IAIFoundryClientFactory in integration tests | S | 2 | Done | P2-002 |
| P2-009 | Create glossary of domain terms | S | 2 | Ready | P2-004 |
| P2-010 | Create sequence diagrams for agent interactions | M | 2 | Ready | P2-006a |
| **Phase 3: Summarization & RAG** |||||
| P3-001 | Summarizer Agent (3 levels) | XL | 3 | Done | P2-005 |
| P3-002 | Azure AI Search vector index | M | 3 | Done | P2-001 |
| P3-003 | Embedding pipeline (text-embedding-3-large) | L | 3 | Done | P3-002 |
| P3-004 | Q&A Agent with RAG (single-shot) | XL | 3 | Done | P3-003 |
| P3-005 | Agentic Q&A with tools (search_sessions, get_session_detail, get_patient_timeline, aggregate_metrics) | L | 3 | Done | P3-004 |
| B-003 | Synthetic data generator script | M | 3 | Ready | P2-004 |
| B-014 | Reindex/backfill job for AI Search | M | 3 | Ready | P3-002 |
| **Pre-Phase 3 Checkpoint (Tabled Items)** |||||
| B-020 | RBAC / Entra ID authentication | L | 3+ | Tabled | - |
| B-021 | Audit logging & compliance | L | 3+ | Tabled | - |
| B-022 | OpenAI cost guardrails (full) | M | 3+ | Tabled | - |
| B-023 | Data lifecycle (SQL + Blob) | M | 3+ | Tabled | - |
| B-024 | Private networking baseline | L | 3+ | Tabled | - |
| **Phase 4: Risk Dashboard & UI** |||||
| P4-001 | Supervisor review dashboard (React frontend + API) | XL | 4 | Done | P3-001 |
| B-059 | Frontend testing infrastructure (Vitest + RTL + MSW + 44 unit tests + CI job) | M | 4 | Done | P4-001 |
| B-060 | Playwright smoke tests for frontend routes (4 tests) | S | 4 | Done | B-059 |
| B-061 | Reorganize frontend tests to `__tests__/` + add Tier 1-2 coverage (~38 new tests) | M | 4 | Done | B-060 |
| B-062 | Frontend Tier 3 test coverage (hooks, Button, summary API — 15 tests) | S | 4 | Done | B-061 |
| P4-002 | Risk trend visualization | L | 4 | Ready | P4-001 |
| P4-003 | Patient history timeline view | L | 4 | Ready | P4-001 |
| P4-004 | Flagged session approve/dismiss workflow | M | 4 | Ready | P4-001 |
| P4-005 | Document upload UI (therapist uploads note → extraction pipeline) | L | 4 | Ready | P4-001 |
| B-063 | Full-stack Playwright E2E tests (browser + real Aspire backend) | L | 4 | Ready | P4-001 |
| **Phase 5: Polish & Testing** |||||
| P5-001 | Integration tests (golden files) | L | 5 | Ready | P2-005 |
| P5-002 | Data flow diagrams (document->agent->DB) | M | 5 | Blocked | B-004 |
| P5-003 | API usage examples | S | 5 | Blocked | P1-019 |
| B-004 | Architecture diagrams (Mermaid) | M | 5 | Blocked | P2-010 |
| B-005 | Load testing setup | M | 5 | Blocked | P5-001 |
| B-015 | Contract tests for API DTOs | M | 5 | Blocked | P1-004 |
| B-016 | Load/concurrency tests | M | 5 | Blocked | P5-001 |
| B-017 | Safety/red-team evals | L | 5 | Ready | P2-005 |
| B-038 | Golden files for non-risk fields | L | 5 | Ready | P2-004 |
| **Phase 6: Deployment** |||||
| P6-001 | Configure dev environment (development Azure resources) | M | 6 | Blocked | P5-001 |
| P6-002 | Configure prod environment (production Azure resources) | M | 6 | Blocked | P6-001 |
| P6-003 | GitHub Actions deploy.yml (app deployment) | M | 6 | Blocked | P6-001 |
| B-029 | Infra drift checks: bicep what-if + validate | M | 6 | Ready | P1-015 |
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
| P1-013 | GitHub Actions CI workflow | 2026-01-30 |
| B-028 | CI quality gates (format, build, test) | 2026-01-30 |
| B-018 | Coverage enforcement (30% threshold, excludes migrations) | 2026-01-30 |
| P1-014 | Branch protection on develop | 2026-01-30 |
| P1-022 | Make repo public | 2026-01-30 |
| B-026 | Configure GitHub OIDC auth for Azure | 2026-01-30 |
| B-027 | GitHub `dev` environment with OIDC secrets | 2026-01-31 |
| P1-015 | Write Bicep IaC from scratch (infra.yml workflow) | 2026-01-31 |
| B-039 | Basic CRUD integration tests (Patient, Session) | 2026-01-31 |
| P2-001 | Azure OpenAI setup (AI Foundry connection + SDK wiring) | 2026-01-31 |
| P2-002 | Model Router implementation (tests added) | 2026-01-31 |
| B-040 | Stub IAIFoundryClientFactory in integration tests | 2026-01-31 |
| P2-003 | Intake Agent (first LLM call, unit tests) | 2026-01-31 |
| P2-004 | Clinical Extractor Agent (parallel 9-section extraction) | 2026-01-31 |
| P2-007 | Confidence scoring (incorporated into P2-004) | 2026-01-31 |
| P2-005 | Risk Assessor Agent with safety-critical validation | 2026-01-31 |
| P2-008 | ExtractionOrchestrator, controllers, Doc Intelligence, FunctionalTests | 2026-02-01 |
| B-041 | Bicep role assignments for Doc Intel + OpenAI | 2026-02-01 |
| B-042 | AI Project → OpenAI connection (aiProjectConnection.bicep) | 2026-02-01 |
| B-043 | Local dev documentation (docs/LOCAL_DEV.md) | 2026-02-01 |
| B-044 | Fix SessionRepository.UpdateAsync concurrency bug (RowVersion + retry) | 2026-02-01 |
| B-045 | Deterministic E2E test runner script (scripts/run-e2e.sh) | 2026-02-01 |
| P2-006a | Agent tools: Core infra + check_risk_keywords + validate_schema | 2026-02-01 |
| P2-006b | ClinicalExtractor agent loop transformation + 3 more tools | 2026-02-01 |
| B-037 | Tool call limit graceful handling (AgentLoopRunner MaxToolCalls=15) | 2026-02-01 |
| - | Planning complete | 2026-01-24 |
| P3-001 | Summarizer Agent (session, patient, practice summaries) | 2026-02-02 |
| P3-002 | Azure AI Search vector index infrastructure | 2026-02-03 |
| P3-003 | Embedding pipeline (text-embedding-3-large) | 2026-02-04 |
| P3-004 | Q&A Agent with RAG (clinical Q&A via vector search + LLM) | 2026-02-05 |
| P3-005 | Agentic Q&A with tools (4 tools + agent loop) | 2026-02-05 |
| B-010 | Exponential backoff for Azure SDK clients (OpenAI/Search/DocIntel) | 2026-02-05 |
| B-050 | Fix fire-and-forget scoped service lifetime in IngestionController | 2026-02-06 |
| B-051 | Add patient-scoping guard to Q&A tools | 2026-02-06 |
| B-052 | Fix OData filter injection in SearchIndexService | 2026-02-06 |
| B-053 | Fail extraction pipeline on JSON parse failure | 2026-02-06 |
| B-054 | Add wall-clock timeout to agent loop (5 min) | 2026-02-06 |
| B-055 | Fix E2E extraction JSON parse failures (resilient deserialization + prompt fix) | 2026-02-06 |
| B-056 | Harden LLM JSON parsing and error handling across all agents | 2026-02-06 |
| B-057 | Add response_format json_object + harden E2E field assertions | 2026-02-06 |
| B-058 | Full 74-field assertion coverage + 4 string→enum conversions | 2026-02-06 |
| P4-001 | Supervisor review dashboard (React frontend + API) | 2026-02-07 |
| B-059 | Frontend testing infrastructure (Vitest + RTL + MSW + 44 unit tests + CI job) | 2026-02-07 |
| B-060 | Playwright smoke tests for frontend routes (4 tests) | 2026-02-07 |
| B-061 | Reorganize frontend tests to `__tests__/` + Tier 1-2 coverage | 2026-02-07 |
| B-062 | Frontend Tier 3 test coverage (hooks, Button, summary API) | 2026-02-07 |

---

## Session Log (Last 5)

| Date | What Happened |
|------|---------------|
| 2026-02-07 | **B-061, B-062 complete.** (B-061) Reorganized frontend tests from `src/pages/__tests__/` to top-level `__tests__/` directory with proper tier structure. Added Tier 1-2 coverage: 38 new tests across 7 files (api/client, api/review, components/ui/Badge, ConfidenceBar, RiskBadge, hooks/useSubmitReview, utils/format). Total: 82 tests. (B-062) Added Tier 3 coverage: 15 new tests across 4 files — usePracticeSummary enabled-guard tests (3), useReviewDetail enabled-guard tests (2), Button variant/passthrough/disabled tests (8), summary API URL encoding tests (2). Total: 97 frontend tests. Tier 4 skipped (Card, Spinner, useReviewQueue, useReviewStats — trivial passthrough, no logic). |
| 2026-02-07 | **P4-001, B-059, B-060 complete.** (P4-001) React supervisor review dashboard: 3 pages (Dashboard, ReviewQueue, SessionDetail), AppShell layout with Sidebar, 6 UI components (Badge, Button, Card, ConfidenceBar, RiskBadge, Spinner), 5 React Query hooks, API client layer, TypeScript types. Routes: `/` (dashboard with practice summary + flagged patients), `/review` (filterable/sortable queue), `/review/session/:id` (extraction detail + review action panel). (B-059) Frontend testing infrastructure: Vitest + happy-dom + RTL + MSW, 44 unit/component tests across 4 files, `renderWithProviders` helper, MSW handlers + fixtures, `scripts/check-frontend.sh`, `frontend-tests` CI job in ci.yml. (B-060) Playwright smoke tests: 4 browser tests (Dashboard stats, Review Queue names, Session Detail + risk section, Sidebar navigation) using `page.route()` with shared fixtures. Added to CI and check-frontend.sh. Created B-061 for test reorganization + Tier 1-2 coverage (planned, ready to implement). |
| 2026-02-06 | **B-058 complete (74-field assertions + 4 string→enum + temperature fix).** Converted 4 free-text string fields to enums: Appearance (MSE), BehaviorType (MSE), DiagnosisChangeType (Diagnoses), DischargePlanningStatus (NextSteps). Updated 3 schema files, ExtractionSchemaGenerator auto-discovers new enums. ExtractionAssertions.cs: removed 3 unused helpers, added AssertFieldPresent helper, added/fixed assertions for all 74 extracted fields (was ~50). Updated stale per-section prompts in ExtractionPrompts.cs. Fixed missing temperature on ClinicalExtractorAgent (spec said 0.1f, lost during AgentLoopRunner refactor) — added temperature parameter to AgentLoopRunner, set 0.1f for extraction, 0.2f for Q&A. Updated clinical-schema.md spec. Removed Sequential collection from E2E tests — classes now run in parallel (Azure SDK backoff handles rate limits). Deleted TestCollections.cs. 616 unit tests. Coverage 82.15%. 8/8 E2E. |
| 2026-02-06 | **B-057 complete (response_format + E2E assertions).** Added `ChatResponseFormat.CreateJsonObjectFormat()` to all JSON-returning agents (ClinicalExtractor, RiskAssessor, Summarizer, Intake) — eliminates "Failed to parse extraction JSON" transient errors. Added `responseFormat` parameter to AgentLoopRunner. Expanded ExtractionAssertions from 5 to ~82 fields across all 9 sections. All interpretive enum fields validate against full enum sets; free-text fields use broad keyword stems; deterministic fields guard against LLM extraction failures. Fixed FluentAssertions `ContainMatch` bug (`\|` treated literally). Fixed Docker cleanup in run-e2e.sh to remove `storage-*` containers before pruning networks. Updated CLAUDE.md with response_format best practice, E2E assertion patterns, and Docker cleanup docs. 616 unit tests. Coverage 82.15%. 8/8 E2E. |
| 2026-02-06 | **B-055 + B-056 complete (LLM JSON hardening).** (B-055) Fixed ClinicalExtractorAgent JSON parsing: schema-embedded prompt via ExtractionSchemaGenerator, resilient deserialization with section-by-section fallback, robust ExtractJson with regex+brace extraction. (B-056) Applied B-055 patterns across all agents: (1) Created shared LlmJsonHelper with ExtractJson, TryParseConfidence, TryParseInt, TryParseDouble. (2) Created RiskSchemaGenerator — generates risk schema from C# types, replaces hardcoded JSON in RiskPrompts. (3) Fixed safety-critical error hiding: ClinicalExtractorAgent.ParseExtractionResponse returns null on empty content (was returning default ClinicalExtraction with all-Low risk); RiskAssessorAgent.ReExtractRiskAsync throws on parse failure (triggers RequiresReview). (4) Fixed deserialization: RiskAssessorAgent handles string-typed confidence and string source; IntakeAgent adds NumberHandling; QAAgent handles string confidence. (5) Added E2E ExtractionAssertions shared helper — all 3 extraction E2E tests now verify field-level clinical data. 15 new unit tests (616 total). Coverage 82.07%. |
| 2026-02-06 | **B-050 to B-054 complete (architecture fixes).** (1) IngestionController fire-and-forget now uses IServiceScopeFactory for proper DI scope. (2) GetSessionDetailTool.AllowedPatientId + SearchSessionsTool.RequiredPatientId prevent cross-patient data access; QAAgent sets scope before each call. (3) SearchIndexService validates patientIdFilter as canonical GUID before OData interpolation. (4) ClinicalExtractorAgent.ParseExtractionResponse returns null on JsonException; orchestrator fails pipeline with DocumentStatus.Failed (safety: prevents false-negative risk assessment). (5) AgentLoopRunner has 5-min linked CancellationTokenSource timeout. 15 new unit tests (570 total). Coverage 82.19%. E2E: 3 extraction tests now correctly fail — pre-existing issue where LLM returns unparseable JSON was previously silently swallowed. Created B-055 to investigate. |
| 2026-02-05 | **B-010 complete.** Added exponential backoff retry configuration for all Azure SDK clients. Created `AzureRetryDefaults` in Core with two overloads: `Configure<T>()` for Azure.Core clients (Search, Doc Intelligence) with 5 retries/1s base/60s max/exponential mode/120s network timeout, and `ConfigureRetryPolicy<T>()` for System.ClientModel clients (OpenAI) with `ClientRetryPolicy(5)`. Applied to AIFoundryClientFactory (+ startup logging), SearchIndexService, and DocumentIntelligenceClient. 8 new unit tests (555 total). Coverage 82.31%. Created B-048 follow-up for circuit breaker. |
| 2026-02-05 | **P3-005 complete.** Agentic Q&A with 4 tools: SearchSessionsTool (hybrid vector+keyword search), GetSessionDetailTool (drill into individual sessions), GetPatientTimelineTool (chronological timeline with risk/mood change detection), AggregateMetricsTool (mood_trend, session_count, intervention_frequency, risk_distribution, diagnosis_history). Added AgentLoopRunner overload for explicit tool lists. QAAgent refactored to dual-path: simple questions → single-shot RAG, complex questions → agentic loop with tools. 43 new unit tests (547 total). Coverage 82.23%. All 8 E2E tests pass. Also added RetryHandler to E2E ApiFixture (single retry on socket/TLS/5xx errors), consolidated LongClient into fixture, removed duplicate HttpClient from QATests. |

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
