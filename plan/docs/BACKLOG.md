# SessionSight Backlog

> **Single source of truth for task tracking.** Update this file every session.

---

## Current Status

**Phase**: Phase 4 (Risk Dashboard & UI) - IN PROGRESS
**Next Action**: B-064 (extraction trigger race condition fix)
**Last Updated**: February 9, 2026

**Milestone**: P4-004 and B-065 complete — review workflow validated end-to-end, fixed-port endpoint config clarified, full regression passed

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
| P4-002 | Risk trend visualization | L | 4 | Done | P4-001 |
| P4-003 | Patient history timeline view | L | 4 | Done | P4-001 |
| P4-004 | Flagged session approve/dismiss workflow | M | 4 | Done | P4-001 |
| P4-005 | Patient/Session/Upload screens (3 pages: /patients, /sessions, /upload) | L | 4 | Done | P4-001 |
| B-063 | Full-stack Playwright E2E tests (browser + real Aspire backend) | M | 4 | Done | P4-005 |
| B-064 | Extraction trigger race condition fix (HOLDLOCK or optimistic concurrency) | S | 2 | Ready | - |
| B-065 | Frontend code coverage: Add Vitest coverage (v8), set 80% threshold, add to check-frontend.sh + CI | S | 4 | Done | B-059 |
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
| P4-002 | Risk trend visualization (patient trend API + dashboard chart + tests) | 2026-02-08 |
| P4-003 | Patient history timeline view (deterministic timeline API + UI + tests) | 2026-02-08 |
| P4-005 | Patient/Session/Upload screens (3 pages + API + tests) | 2026-02-07 |
| B-063 | Full-stack Playwright E2E tests (browser + real Aspire backend) | 2026-02-07 |
| P4-004 | Flagged session approve/dismiss workflow | 2026-02-09 |
| B-065 | Frontend code coverage: Add Vitest coverage (v8), set 80% threshold, add to check-frontend.sh + CI | 2026-02-09 |

---

## Session Log (Last 5)

| Date | What Happened |
|------|---------------|
| 2026-02-09 | **P4-004 and B-065 complete.** Closed remaining gaps by adding smoke Playwright coverage for approve and dismiss actions on `/review/session/:sessionId` with request payload assertions and success-state verification, and by fixing a stale full-stack empty-state assertion to match current Review Queue UI copy. Clarified fixed-port endpoint ownership to avoid Aspire duplicate-endpoint startup failure: AppHost `.WithHttpsEndpoint(7039, name: "https")` left as commented reference while `src/SessionSight.Api/Properties/launchSettings.json` remains source of truth for `https://localhost:7039`. Validation path passed: `./scripts/check-frontend.sh`, `dotnet test --filter "FullyQualifiedName~ReviewControllerTests"` (12/12), `./scripts/run-e2e.sh --frontend` (3 passed, 1 skipped), and `./scripts/run-e2e.sh --all` (backend functional 8/8 + frontend full-stack 3 passed, 1 skipped). Frontend coverage currently reports 88.64% lines, 84.41% branches, 88.04% statements, 85.71% functions (threshold remains 82%, exceeding B-065 target of 80%). |
| 2026-02-08 | **P4-003 complete.** Implemented patient timeline end-to-end with deterministic API data only (no new LLM dependency for rendering). Backend: added `GET /api/summary/patient/{patientId}/timeline` in `SummaryController` with date-range validation, chronological ordering, deterministic risk/mood change computation, document/review metadata, and new `TimelineDtos`. Frontend: added timeline types, `getPatientTimeline` API function, `usePatientTimeline` + `usePatient` hooks, new `/patients/:patientId/timeline` route + `PatientTimeline` page, and patient-table timeline links. Tests: added backend controller tests for not-found/invalid-range/repository path/field computation; added frontend API/hook/page tests plus patients-page link test; updated smoke Playwright with timeline navigation test; extended full-stack Playwright flow to assert timeline page. Validation sequence run in order: `dotnet test --filter "Category!=Functional"` (pass), `./scripts/check-frontend.sh` (pass), `./scripts/run-e2e.sh --frontend` (pass: 3 passed, 1 skipped), `./scripts/run-e2e.sh --all` (pass: backend functional 8/8 + frontend full-stack 3 passed, 1 skipped). |
| 2026-02-08 | **P4-002 complete.** Implemented risk trend visualization end-to-end. Backend: added `GET /api/summary/patient/{patientId}/risk-trend` in `SummaryController` with deterministic risk scoring (Low=0, Moderate=1, High=2, Imminent=3), latest-risk + escalation metadata, and new DTOs. Frontend: added risk trend types, summary API function, `usePatientRiskTrend` hook, `RiskTrendChart` SVG component, and dashboard integration with flagged-patient selector, loading/error/empty states, and trend metadata badges. Tests: added backend API tests for not-found/invalid-range/order/scoring/escalation; added frontend API/hook/dashboard tests; updated smoke Playwright; added full-stack Playwright dashboard risk-trend assertion. Validation sequence run in order: `dotnet test --filter "Category!=Functional"` (pass), `./scripts/check-frontend.sh` (pass), `./scripts/run-e2e.sh --frontend` (pass), then `./scripts/run-e2e.sh --all` (pass: backend functional 8/8 and frontend full-stack 3 passed, 1 skipped). |
| 2026-02-07 | **B-063 complete.** Full-stack Playwright E2E tests: Created `e2e/full-stack/upload-flow.spec.ts` with 3 tests (complete upload flow, error handling, review queue). Tests hit real Aspire backend with LLM extraction (~2 min, ~$0.05-0.10 per run). Combined `run-fullstack-e2e.sh` into `run-e2e.sh` with `--frontend` flag. Added `fullStack` Playwright project with 180s action timeout, 5 min test timeout. Fixed React crash bug: `ExtractedField.source` was typed as `string` but backend returns `SourceMapping` object `{text, startChar, endChar, section}` — added `SourceMapping` interface, updated `SessionDetail.tsx` to render `source?.text`, updated fixtures. Updated CLAUDE.md, LOCAL_DEV.md with new E2E commands. 128 frontend tests, 631 backend tests. |
| 2026-02-07 | **P4-005 complete.** Patient/Session/Upload screens: 3 new pages (`/patients`, `/sessions`, `/upload`), 5 API functions (patients.ts, sessions.ts, upload.ts), 5 React Query hooks (usePatients, useCreatePatient, useSessions, useCreateSession, useUploadDocument), updated navigation (Sidebar, MobileNav, App routes). Backend: added `ISessionRepository.GetAllAsync(patientId?, hasDocument?)`, `GET /api/sessions` endpoint with filters, `HasDocument` property on SessionDto. Tests: 31 new frontend tests (api/patients, api/sessions, api/upload, hooks/usePatients, hooks/useSessions, pages/Patients, pages/Sessions, pages/Upload), 3 new backend tests (SessionsController.GetAll). Fixed pre-existing type error in SessionDetail.tsx (source.text on string type). Added MSW fixtures (patients.ts, sessions.ts) and handlers. Updated CLAUDE.md with Test Structure section documenting all test types and paths. 128 frontend tests, 132 backend tests. Coverage 82.32%. Created B-065 for frontend coverage enforcement (80%). |

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
