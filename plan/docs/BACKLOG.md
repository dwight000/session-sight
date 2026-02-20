# SessionSight Backlog

> **Single source of truth for task tracking.** Update this file every session.

---

## Current Status

**Phase**: Phase 6 (Deployment) - IN PROGRESS
**Next Action**: B-090 (Document validation review-routing)

**Last Updated**: February 20, 2026

**Milestone**: P6-007 complete — demo data seeding with 8 patients + full extraction pipeline. 6 of 9 gap audit items done (B-085, B-086, B-087, B-088, B-089, B-093). Remaining: B-090, B-091, B-092.

---

## Active Work

<!-- When you start a task, move it here. Only ONE task at a time. -->

*(none — pick next from Task Table)*

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
| B-032 | Document size validation (reject >30 pages) | M | 2 | Done | P2-008 |
| B-033 | Internal service auth (Function->API) | M | 2 | Ready | P2-008 |
| B-034 | Fix idempotency race condition (SQL MERGE with HOLDLOCK) | M | 2 | Done | P2-008 |
| B-035 | Synchronous AI Search indexing (user-visible after B-085) | M | 2 | Ready | P2-004 |
| B-036 | Document Intelligence failure handling | M | 2 | Ready | P2-008 |
| B-048 | Circuit breaker for Azure SDK clients (Polly or custom HttpPipelinePolicy) | M | 2 | Done | B-010 |
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
| B-046 | Add local API file logging (Serilog) to `/tmp/sessionsight/` + update debug docs/scripts | S | 1 | Done | - |
| B-047 | Replace Aspire with Docker Compose ([plan](../../.claude/plans/replace_aspire_docker_compose_draft.md)) | M | 1 | Tabled | - |
| B-066 | Remove temporary DIAG_LOG hack (`/tmp/api-diag.log`) and legacy docs/scripts after Serilog validation | S | 1 | Done | - |
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
| B-064 | Extraction trigger race condition fix (HOLDLOCK or optimistic concurrency) | S | 2 | Done | - |
| B-065 | Frontend code coverage: Add Vitest coverage (v8), set 80% threshold, add to check-frontend.sh + CI | S | 4 | Done | B-059 |
| **Phase 5: Polish & Testing** |||||
| P5-001 | Integration tests (golden files) | L | 5 | Done | P2-005 |
| P5-002 | Data flow diagrams (document->agent->DB) | M | 5 | Blocked | B-004 |
| P5-003 | API usage examples | S | 5 | Ready | - |
| B-004 | Architecture diagrams (Mermaid) | M | 5 | Blocked | P2-010 |
| B-005 | Load testing setup | M | 5 | Done | - |
| B-015 | Contract tests for API DTOs | M | 5 | Ready | - |
| B-016 | Load/concurrency tests | M | 5 | Done | B-005 |
| B-070 | Merge redundant E2E extraction tests into shared collection fixture | S | 5 | Done | - |
| B-017 | Safety/red-team evals (14 adversarial golden files) | L | 5 | Done | P2-005 |
| B-071 | Prompt hardening: euphemistic language → active SI classification | S | 5 | Done | B-017 |
| B-038 | Golden files for non-risk fields | L | 5 | Done | P2-004 |
| B-068 | Add prompt rule: infer si_frequency from severity when evidence absent | S | 5 | Done | P5-001 |
| B-069 | Investigate extraction timeout (300s HttpClient.Timeout in golden cases) | S | 5 | Done | P5-001 |
| **Phase 6: Deployment** |||||
| P6-001 | Configure dev environment (development Azure resources) | M | 6 | Done | - |
| P6-002 | Configure stage environment (pre-production Azure resources) | M | 6 | Done | - |
| P6-003 | GitHub Actions deploy.yml (app deployment) | M | 6 | Done | - |
| B-029 | Infra drift checks: bicep what-if + validate | M | 6 | Done | P1-015 |
| B-067 | Validate hosted cloud log ingestion (App Insights) + troubleshooting playbook and query pack | M | 6 | Done | P6-003 |
| B-072 | Cloud database seeding (dev): Therapist FK constraint blocks session creation | S | 6 | Done | - |
| B-073 | Add `deployContainerApps`/`ghcrToken` inputs to infra.yml workflow | S | 6 | Done | - |
| B-074 | Automate EF migrations in deploy.yml (run after image update) | M | 6 | Done | - |
| B-075 | Fix CRLF line endings in repo (renormalize to LF per .gitattributes) | S | 6 | Done | - |
| B-076 | Sync SQL connection string after infra deploy (prevent password drift) | S | 6 | Done | - |
| B-077 | Switch to Managed Identity for SQL auth (eliminate password sync) | M | 6 | Done | - |
| B-078 | Fix nginx 413 error: add client_max_body_size for file uploads | S | 6 | Done | - |
| B-030 | Promotion model: dev->stage approval rules | M | 6 | Done | - |
| B-031 | Rollback strategy: keep last good artifact | M | 6 | Done | P6-003 |
| P6-004 | Environment-specific configuration | M | 6 | Done | P6-002 |
| P6-005 | Create GitHub Release with SemVer tag (v1.0.0) | S | 6 | Done | P6-003 |
| P6-006 | Enable Dependabot for dependency updates | S | 6 | Done | P6-005 |
| B-079 | Fix concurrent role assignment conflicts in Bicep (dependsOn ordering) | S | 6 | Done | - |
| B-080 | Store ghcrToken as GitHub secret (eliminate manual input for deployContainerApps) | S | 6 | Done | - |
| B-081 | Review and merge Dependabot PRs (~20 pending) | M | 6 | Done | - |
| B-082 | Fix BlobNotFound + stuck Processing + file types + sample documents on Upload page | M | 6 | Done | - |
| B-083 | Bump Azure OpenAI TPM, decouple extraction from HTTP lifecycle, fix retry UI, enable /health | S | 6 | Done | - |
| B-084 | Move extraction to background queue (decouple from HTTP request thread) | L | 6 | Backlog | - |
| P6-007 | Demo data and walkthrough | M | 6 | Done | - |
| **Gap Audit Items (B-085–B-093)** |||||
| B-085 | Q&A Chat UI (patient-scoped clinical Q&A page) | L | 4 | Done | - |
| B-086 | Patient longitudinal summary on timeline page | M | 4 | Done | - |
| B-087 | Practice summary diagnosis/intervention breakdown on Dashboard | S | 4 | Done | - |
| B-088 | Session summary regeneration button on SessionDetail | S | 4 | Done | - |
| B-089 | Delete/replace uploaded document | S | 4 | Done | - |
| B-090 | Document validation review-routing (handwriting, OCR confidence, language) | M | 2 | Ready | - |
| B-091 | RAG eval harness (precision@5, human eval record) | M | 5 | Ready | - |
| B-092 | Phase 2 SLO measurement (latency, F1, cost-per-note) | S | 5 | Ready | - |
| B-093 | Compare sessions tool for QA agent | S | 3 | Done | - |

---

## Task Detail Notes

### B-085 Details (Q&A Chat UI)

**Problem:** The QAAgent with 4 agentic tools (`SearchSessionsTool`, `GetSessionDetailTool`, `GetPatientTimelineTool`, `AggregateMetricsTool`) and hybrid vector+keyword search is the most technically complex backend subsystem — but has zero frontend consumer. The only way to use it is via `curl POST /api/qa/patient/{patientId}`. Discovered during gap audit (2026-02-19).

**Scope:**
- New `/qa` route and page component with patient selector dropdown (reuse `usePatients` hook) and chat-style message input
- Frontend API client (`api/qa.ts`) calling `POST /api/qa/patient/{patientId}` with body `{ question }`
- `useAskQuestion` mutation hook following existing `useMutation` + `invalidateQueries` pattern
- Answer display with cited source sessions (response includes `answer`, `sources[]`, `complexity`, `model`)
- Loading state appropriate for 5-30s agent processing time
- Sidebar nav link (add `{ to: '/qa', label: 'Q&A' }` to `Sidebar.tsx` links array)
- Unit tests (component + hook) + Playwright smoke test

**Existing infra to reuse:**
- `fetchApi` pattern from `api/client.ts`
- `usePatients` hook for patient selector dropdown
- `<Card>`, `<Spinner>`, `<Badge>` UI components
- TanStack React Query `useMutation` pattern (see `useCreatePatient` for reference)

**Files:** New `src/SessionSight.Web/src/api/qa.ts`, new `src/SessionSight.Web/src/hooks/useAskQuestion.ts`, new `src/SessionSight.Web/src/pages/QA.tsx`, `src/SessionSight.Web/src/App.tsx` (add route), `src/SessionSight.Web/src/components/layout/Sidebar.tsx` (add nav link).

**Acceptance:** User selects patient, types question, sees RAG-grounded answer with source citations. Smoke test verifies route loads and input renders.

### B-086 Details (Patient Longitudinal Summary on Timeline Page)

**Problem:** `GET /api/summary/patient/{id}` returns a `PatientSummary` with `progressNarrative`, `moodTrend`, `effectiveInterventions`, `ongoingConcerns`, and `recommendations` — the most clinically valuable longitudinal insight. But `PatientTimeline.tsx` never calls this endpoint. The page jumps straight from a date filter into raw session cards with no synthesized overview. Discovered during gap audit (2026-02-19).

**Scope:**
- New API client function `getPatientSummary(patientId)` in `api/summary.ts`
- New `usePatientSummary` query hook (`queryKey: ['patientSummary', patientId]`)
- Summary card/panel at top of `PatientTimeline.tsx`, above the session list, showing:
  - Progress narrative (prose block)
  - Mood trend badge
  - Effective interventions list
  - Ongoing concerns
  - Recommendations
- Loading/error states following existing `isLoading → <Spinner />`, `error → red div` pattern

**Files:** `src/SessionSight.Web/src/api/summary.ts`, new hook file, `src/SessionSight.Web/src/pages/PatientTimeline.tsx`.

**Acceptance:** PatientTimeline page shows longitudinal summary panel above session cards. Panel gracefully handles patients with no extraction data.

### B-087 Details (Practice Summary Breakdown on Dashboard)

**Problem:** `Dashboard.tsx` already fetches `PracticeSummary` via `usePracticeSummary`, but only renders 5 of the available fields (`totalSessions`, `totalPatients`, `averageSessionsPerPatient`, `riskDistribution`, `flaggedPatients`). Three additional fields are fetched and discarded: `topInterventions[]`, `sessionsRequiringReview`, `generatedAt`. Discovered during gap audit (2026-02-19).

**Scope:**
- Add `sessionsRequiringReview` as a stat card (e.g., "Pending Review" count) on Dashboard
- Add `topInterventions[]` as a table or bar chart showing intervention frequency breakdown
- No backend changes — data already in the API response type

**Files:** `src/SessionSight.Web/src/pages/Dashboard.tsx`, possibly a new chart/table component.

**Acceptance:** Dashboard shows intervention frequency breakdown and pending review count alongside existing stats.

### B-088 Details (Session Summary Regeneration Button)

**Problem:** `GET /api/summary/session/{id}?regenerate=true` supports on-demand summary regeneration via the `SummaryController`, but `SessionDetail.tsx` reads the stored `summaryJson` from the review DTO (`data.summaryJson`) and parses it inline. There is no way for a supervisor to refresh a stale or poor-quality summary from the UI. Discovered during gap audit (2026-02-19).

**Scope:**
- API client function `getSessionSummary(sessionId, regenerate?)` in `api/summary.ts`
- "Regenerate Summary" button on `SessionDetail.tsx` with loading state
- On click: call `GET /api/summary/session/{id}?regenerate=true`, replace displayed summary with fresh result

**Files:** `src/SessionSight.Web/src/api/summary.ts`, `src/SessionSight.Web/src/pages/SessionDetail.tsx`.

**Acceptance:** Supervisor clicks regenerate, sees loading spinner, then updated summary replaces the old one.

### B-089 Details (Delete/Replace Uploaded Document)

**Problem:** No `DELETE` endpoint exists for session documents. If a user uploads the wrong file to a session, it is permanent — there is no way to remove it and re-upload. The only option is to create a new session entirely. Discovered during gap audit (2026-02-19).

**Scope:**
- Backend `DELETE /api/sessions/{sessionId}/document` endpoint in `DocumentsController`
  - Remove blob via `IDocumentStorage.DeleteAsync`
  - Reset or delete `SessionDocument` record
  - Reset extraction result if one exists (set `ExtractionResult` to null, status back to Pending)
- Add `DeleteAsync(string blobUri)` to `IDocumentStorage` / `AzureBlobDocumentStorage`
- Frontend delete button on session detail or upload flow
- Backend unit tests + frontend tests

**Files:** `src/SessionSight.Api/Controllers/DocumentsController.cs`, `src/SessionSight.Core/Interfaces/IDocumentStorage.cs`, `src/SessionSight.Infrastructure/Storage/AzureBlobDocumentStorage.cs`, frontend session/upload pages.

**Acceptance:** User can delete a wrongly-uploaded document and upload the correct one. Extraction state is properly reset.

### B-090 Details (Document Validation Review-Routing)

**Problem:** Phase 2 spec describes flagging documents for review when: (1) handwriting >30% of content, (2) OCR confidence <70% average, (3) non-English content detected. None of these checks were implemented. The Azure Document Intelligence `AnalyzeResult` already returns all three signals in the existing API response — `result.Styles[].IsHandwritten` + `Confidence`, `result.Languages[].Locale` + `Confidence`, and per-word confidence scores — but `DocumentIntelligenceParser` discards them. No additional Azure API calls are needed. Discovered during gap audit (2026-02-19).

**Scope:**
- Enrich `ParsedDocumentMetadata` with new properties:
  - `HandwritingPercentage` (float) — ratio of handwritten text spans to total
  - `DetectedLanguages` (list) — language locales with confidence scores
  - `MinPageConfidence` (float) — lowest per-page average word confidence
- In `DocumentIntelligenceParser.ParseAsync`:
  - Read `result.Styles` where `IsHandwritten == true`, compute handwriting span ratio
  - Read `result.Languages` for locale detection
  - Compute per-page word confidence distribution (not just overall average)
- In `ExtractionOrchestrator`, after parsing check thresholds:
  - Handwriting > 30% → set `RequiresReview = true` with reason
  - MinPageConfidence < 0.70 → set `RequiresReview = true` with reason
  - Primary language not English → set `RequiresReview = true` with reason
- Pipeline continues normally (process but flag)

**Files:** `src/SessionSight.Agents/Services/DocumentIntelligenceParser.cs`, `src/SessionSight.Agents/Models/ParsedDocument.cs`, `src/SessionSight.Agents/Orchestration/ExtractionOrchestrator.cs`.

**Acceptance:** A handwritten document, low-OCR document, or non-English document triggers `RequiresReview` flag and appears in the review queue with the appropriate reason. Existing non-flagged documents are unaffected.

### B-091 Details (RAG Eval Harness)

**Problem:** Phase 3 spec promises `precision@5 > 0.80` and human eval of 20 test queries. PROJECT_PLAN SLO table claims measurable performance targets. No test infrastructure or recorded results exist. The spec references `dotnet test --filter "Category=RAGEval"` as a verification command — this filter matches nothing. Discovered during gap audit (2026-02-19).

**Scope:**
- New golden file directory `plan/data/synthetic/golden-files/qa-eval/` with 20 labeled Q&A cases
- Each case: `{ question, note_content, expected_answer_keywords[], expected_source_fields[], complexity }`
- New `GoldenQACaseProvider` extending `GoldenCaseProviderBase` patterns (reuse `FindRepositoryRoot`, `SelectDeterministicSubset`, mode parsing, `GOLDEN_MODE`/`GOLDEN_FILTER` env vars)
- New `GoldenQAEvalTests` class with `[Trait("Category", "Functional")]`
- Each test: create patient → upload note (via `GoldenTestHelpers.CreatePdfDocument`) → extract (via `LongClient`) → call `POST /api/qa/patient/{id}` → assert answer contains expected keywords, sources reference correct sessions
- Compute and log precision@5 and answer relevance in test output

**Existing infra to reuse:** `GoldenCaseProviderBase` (root finder, deterministic subset, mode parsing), `GoldenTestHelpers.CreatePdfDocument` (in-memory PDF from text), `ApiFixture` (Client + LongClient), `ExtractionAssertions` match modes (`ContainsAny`, `AnyKeyword`), `SharedExtractionFixture` pattern.

**Files:** New `tests/SessionSight.FunctionalTests/GoldenQAEvalTests.cs`, new `Fixtures/GoldenQACaseProvider.cs`, new `plan/data/synthetic/golden-files/qa-eval/*.json`.

**Acceptance:** `./scripts/run-e2e.sh --filter "GoldenQAEvalTests"` runs 20 Q&A evals. Precision@5 and answer relevance metrics are computed and reported in test output.

### B-092 Details (Phase 2 SLO Measurement)

**Problem:** Phase 2 spec promises three SLOs: (1) extraction P95 latency < 30s, (2) extraction F1 > 0.85, (3) cost per note < $0.50. No measurement infrastructure exists anywhere. These are listed in the PROJECT_PLAN.md success criteria table but have no evidence backing them. Discovered during gap audit (2026-02-19).

**Scope:**
- Document measurement methodology for each SLO
- (1) **Latency**: Add `Stopwatch` timing to E2E extraction tests, log P95 across golden file runs. Alternatively parse from Serilog API logs (`/tmp/sessionsight/api/`).
- (2) **F1**: Compute from golden file pass/fail rates. The 74-field E2E assertions already run — aggregate pass/fail into precision/recall/F1 per field category.
- (3) **Cost**: Estimate from Azure OpenAI token usage (available in response headers `x-ratelimit-remaining-tokens` or via Azure portal cost analysis). Could be a script that parses API logs for token counts.
- Record baseline values in a results section (either in test output or a doc).

**Files:** Potentially `tests/SessionSight.FunctionalTests/` (timing additions), `docs/SLO_BASELINES.md` or similar for recorded values.

**Acceptance:** Each SLO has a documented measurement method and a recorded baseline value that can be referenced from the PROJECT_PLAN.

### B-093 Details (Compare Sessions Tool for QA Agent)

**Problem:** The agent-tool-callbacks spec listed 3 summarizer tools: `get_mood_trend`, `identify_effective_interventions`, and `compare_sessions`. Audit found the first two already exist as metrics in `AggregateMetricsTool` (`mood_trend` and `intervention_frequency`). But `compare_sessions` — structured diff between any two specific sessions by ID — has no equivalent. `GetPatientTimelineTool` shows adjacent session deltas but doesn't support arbitrary two-session comparison. Discovered during gap audit (2026-02-19).

**Scope:**
- New `CompareSessionsTool : IAgentTool` in `src/SessionSight.Agents/Tools/`
- Input schema: `{ sessionIdA: string, sessionIdB: string }`
- Behavior: fetch both sessions via `ISessionRepository.GetByIdAsync`, produce structured diff across key fields (mood score, risk level, interventions used, diagnosis, progress rating, presenting concern)
- Register as `Scoped` in `Program.cs` DI
- Add to QA agent's explicit tool list in `QAAgent.cs`
- Unit tests following `GetSessionDetailTool` test patterns

**Existing patterns to follow:** `GetSessionDetailTool` (fetch by ID, return structured data), `IAgentTool` interface with `BinaryData InputSchema`, `ToolResult.Error()` for invalid inputs.

**Files:** New `src/SessionSight.Agents/Tools/CompareSessionsTool.cs`, `src/SessionSight.Api/Program.cs` (DI), `src/SessionSight.Agents/Agents/QAAgent.cs` (tool list), new test file.

**Acceptance:** QA agent can answer "How did session X compare to session Y?" using the tool. Unit test verifies diff output structure.

### B-084 Details (Background Extraction Queue)

**Problem:** The extraction pipeline (intake → clinical extractor → risk assessor → summarizer → embedding) takes 30-120+ seconds. Currently it runs synchronously inside the HTTP POST `/api/extraction/{sessionId}` request thread. This causes:
1. **Client disconnect kills extraction** — if the user navigates away, the browser aborts the fetch, ASP.NET Core fires `HttpContext.RequestAborted`, and the CancellationToken propagates through the entire LLM pipeline, canceling mid-flight. B-083 works around this with `CancellationToken.None` but the extraction still blocks the HTTP thread.
2. **HTTP timeout risk** — long extractions risk hitting proxy/ingress/Kestrel timeouts (Container Apps default 240s, but Azure OpenAI retries can push total time past that).
3. **Thread starvation** — each extraction holds a Kestrel thread for 60-120s, limiting concurrent request capacity.
4. **No retry on infrastructure failure** — if the container restarts mid-extraction, the work is lost. A queue provides at-least-once delivery.

**Proposed architecture:**
- **POST `/api/extraction/{id}`** becomes fire-and-forget: transitions status to Processing, enqueues message, returns 202 Accepted immediately
- **Azure Storage Queue** (already provisioned via Aspire) holds extraction jobs
- **Background worker** (`IHostedService` or Azure Functions queue trigger) dequeues and runs the pipeline with its own CancellationToken/timeout
- **Frontend polls** `GET /api/sessions/{id}/extraction` on an interval (or uses SignalR) to show progress
- **Retry:** Queue visibility timeout handles transient failures; poison queue for permanent failures

**Scope:** API controller change, new queue worker service, frontend polling UI, remove synchronous extraction path.

**Dependencies:** None (Azure Storage Queue already available in Aspire setup).

### B-046 Details (Local Logging Baseline)
- Scope: Configure API host logging so local debugging does not depend on temporary DIAG_LOG hacks.
- Logging destination: `/tmp/sessionsight/` parent with subfolders (`api/`, `aspire/`, `vite/`); rolling API log files in `api/` with 7-day retention.
- Behavior: Plain-text readable logs for local use, plus request/response logging toggle via config setting.
- Documentation: Update `.claude/CLAUDE.md`, `docs/LOCAL_DEV.md`, and relevant scripts to show standard triage commands and log file locations.
- Acceptance: During local runs (`start-dev`, `start-aspire`, `run-e2e`), log hints are visible and actionable; failures can be debugged from documented log paths without ad-hoc instructions.

### B-066 Details (Hack Removal)
- Scope: Remove `DIAG_LOG`/`api-diag.log` temporary debug path after B-046 is validated.
- Code cleanup: Remove `DiagLogAsync` helper and any call sites.
- Docs cleanup: Remove legacy references to DIAG_LOG from agent docs/scripts once B-046 guidance is in place.
- Acceptance: Repository no longer relies on DIAG_LOG for normal troubleshooting; grep for `DIAG_LOG`/`api-diag.log` only finds historical backlog text.

### B-067 Details (Cloud Logging Validation)
- Scope: After hosted deployment exists (depends on P6-003), validate that application logs are queryable in Application Insights.
- Validation: Confirm end-to-end log ingestion, useful correlation fields, and practical query snippets for common production issues.
- Playbook: Add cloud troubleshooting steps (where to look, sample queries, expected signals, and failure signatures), including a local-to-cloud triage mapping from `/tmp/sessionsight/{aspire,vite,api}` to App Insights queries.
- Acceptance: Hosted app troubleshooting can be performed without local `/tmp` files; cloud playbook is documented for both Codex and Claude sessions.

### B-068 Details (si_frequency Inference Prompt Rule)
- Context: Case risk-test-005 showed the LLM defaulting `si_frequency` to "Rare" when the note doesn't explicitly state frequency, despite ActiveWithPlan + lethal means + acute crisis.
- Proposed rule: "When `suicidal_ideation` is ActiveWithPlan/ActiveNoPlan but `si_frequency` evidence is absent, infer at minimum Occasional."
- Impact: Prevents clinically implausible low-frequency + high-severity combinations.
- Scope: Add rule to both `ExtractionPrompts.cs` and `RiskPrompts.cs`.
- Currently mitigated by widened golden accepted values for case 005.

### B-070 Details (Merge Redundant E2E Extraction Tests)
- **Problem**: Three functional test classes each run their own standalone extraction of the same `sample-note.pdf` through the full pipeline (IntakeAgent + ClinicalExtractorAgent + RiskAssessorAgent + SummarizerAgent + EmbeddingService). That's 3 identical ~$0.03 extractions ($0.09 total) and ~6 minutes wall-clock for what could be 1 extraction ($0.03, ~2 minutes).
- **Tests to merge**:
  1. `ExtractionPipelineTests.Pipeline_FullExtraction_ReturnsSuccess` — extracts `sample-note.pdf`, asserts 74 fields via `ExtractionAssertions`
  2. `QATests.QA_AnswersQuestionAboutExtractedSession` — extracts `sample-note.pdf`, then runs Q&A asking "What was discussed in the therapy session?"
  3. `SearchIndexTests.Extraction_IndexesSessionWithEmbedding` — extracts `sample-note.pdf`, then queries Azure AI Search for the indexed document with 3072-dim embedding
- **Current structure**: All three classes use `IClassFixture<ApiFixture>`, but `ApiFixture` only provides HTTP clients — no shared extraction state. Each test independently creates a patient, session, uploads the PDF, and triggers `POST /api/extraction/{sessionId}`.
- **Proposed fix**: Use an xUnit **Collection Fixture** pattern:
  1. Create `SharedExtractionFixture` that runs the extraction once in its async lifecycle (`InitializeAsync`): create patient, create session, upload `sample-note.pdf`, trigger extraction, store `sessionId`/`patientId`/extraction response.
  2. Create `[CollectionDefinition("SharedExtraction")]` collection class referencing the fixture.
  3. Move the three test methods into a single `SharedExtractionTests` class (or keep separate classes all decorated with `[Collection("SharedExtraction")]`).
  4. Each test reads from the shared fixture's stored IDs instead of running its own extraction.
- **Files to modify**:
  - `tests/SessionSight.FunctionalTests/ExtractionPipelineTests.cs` — remove `Pipeline_FullExtraction_ReturnsSuccess`, keep the 3 non-LLM tests
  - `tests/SessionSight.FunctionalTests/QATests.cs` — refactor to use shared fixture
  - `tests/SessionSight.FunctionalTests/SearchIndexTests.cs` — refactor `Extraction_IndexesSessionWithEmbedding` to use shared fixture, keep `SearchIndex_HasCorrectSchema` as-is
  - `tests/SessionSight.FunctionalTests/Fixtures/SharedExtractionFixture.cs` — new file
- **Savings**: ~$0.06/run, ~4 minutes wall-clock per E2E run. Adds up across iterations and CI.
- **Risk**: If one test's assertions fail, it won't affect other tests since they share the extraction read-only. The fixture runs the extraction once; tests only do GET requests plus Q&A/search queries.
- **Acceptance**: `./scripts/run-e2e.sh` passes with 2 fewer extractions (verify via API logs showing only 1 `POST /api/extraction` for `sample-note.pdf`).

### B-071 Details (Euphemistic Language Prompt Hardening)
- **Context**: Red-team case risk-test-050 (78F patient using only euphemistic language: "go to sleep and not wake up", "ready for the long rest", "be with Harold again") was classified as Passive SI despite active preparations: researching "peaceful" options, asking pharmacist about medication interactions, distributing possessions, canceling future appointments, declining safety plan.
- **LLM reasoning**: "There is no explicit mention of active suicidal thoughts or plans, so Active levels are not assigned."
- **Issue**: The classification system relies on explicit suicide language for Active categories. Euphemistic language + concrete preparatory behavior should escalate to at least ActiveNoPlan.
- **Proposed rule**: Add to `ExtractionPrompts.cs` and `RiskPrompts.cs`: "When a patient uses euphemistic or indirect language about ending their life (e.g., 'going to sleep permanently', 'ready for the long rest', 'be with [deceased person]') AND exhibits concrete preparatory behaviors (researching methods, giving away possessions, settling affairs, canceling future plans), classify as at minimum ActiveNoPlan even without explicit suicide language."
- **Risk level was correct**: The LLM correctly classified `risk_level_overall` as High due to behavioral warning signs — so the safety-critical risk reporting was not compromised. This is an SI classification refinement.
- **Validation**: After prompt change, re-run `GOLDEN_FILTER=risk-test-050 ./scripts/run-e2e.sh` and tighten accept to `["active_no_plan", "active_with_plan", "active_with_intent"]`.

### B-072 Details (Cloud Database Seeding - Dev)
- **Related**: P6-007 covers the same issue for prod environment.
- **Problem**: Creating a session in the dev cloud environment fails with HTTP 500:
  ```
  The INSERT statement conflicted with the FOREIGN KEY constraint "FK_Sessions_Therapists_TherapistId".
  The conflict occurred in database "sessionsight", table "dbo.Therapists", column 'Id'.
  ```
- **Root cause**: Cloud Azure SQL database has schema (EF migrations ran) but no seed data. The `Sessions` table requires a valid `TherapistId` foreign key, but the `Therapists` table is empty.
- **Local behavior**: `start-dev.sh` seeds a test therapist (`00000000-0000-0000-0000-000000000001`) and sample patients automatically. Cloud has no equivalent seeding mechanism.
- **Affected functionality**: Cannot create sessions, upload documents, or test extraction pipeline in cloud environment.
- **Options**:
  1. **Manual SQL seed** (quick fix): Run INSERT via Azure Portal Query Editor or sqlcmd
  2. **API seed endpoint** (dev only): Add `POST /api/seed` endpoint gated by `ASPNETCORE_ENVIRONMENT=Development`
  3. **EF seed in migrations** (production-safe): Add seed data via `HasData()` in DbContext `OnModelCreating`
  4. **Startup seed service** (environment-aware): `IHostedService` that seeds on startup if DB is empty and env is dev
- **Recommended**: Option 3 or 4 — ensures any fresh deployment has minimum viable data without manual intervention.
- **SQL for manual fix** (from `start-dev.sh`):
  ```sql
  IF NOT EXISTS (SELECT 1 FROM Therapists WHERE Id = '00000000-0000-0000-0000-000000000001')
      INSERT INTO Therapists (Id, Name, LicenseNumber, Credentials, IsActive, CreatedAt)
      VALUES ('00000000-0000-0000-0000-000000000001', 'Test Therapist', 'LIC-001', 'PhD', 1, GETUTCDATE())
  ```
- **Acceptance**: Cloud environment allows creating patients and sessions without FK errors; extraction pipeline can be tested end-to-end.

### B-080 Details (Store ghcrToken as GitHub Secret)
- **Problem**: Running `infra.yml` with `deployContainerApps=true` requires manually passing a GitHub PAT via the `ghcrToken` workflow input. The token lives in Azure Key Vault (`sessionsight-kv-dev`, secret name `ghcr-token`) but must be copy-pasted into the workflow dispatch UI each time. This is error-prone and blocks automation.
- **Current flow**: `ghcrToken` is a `workflow_dispatch` string input → passed to Bicep → stored as container app secret `ghcr-token` → used by container runtime to pull images from `ghcr.io/dwight000/`.
- **Proposed fix**:
  1. Add `GHCR_TOKEN` as a GitHub repository secret (Settings → Secrets → Actions)
  2. Update `infra.yml` to use `${{ secrets.GHCR_TOKEN }}` as default when `ghcrToken` input is empty
  3. Keep the input as an override for rotation scenarios
- **Code change** (`.github/workflows/infra.yml`):
  ```yaml
  # In deploy step, replace:
  --parameters ghcrToken='${{ github.event.inputs.ghcrToken }}'
  # With:
  --parameters ghcrToken='${{ github.event.inputs.ghcrToken || secrets.GHCR_TOKEN }}'
  ```
- **Token value**: `az keyvault secret show --vault-name sessionsight-kv-dev --name ghcr-token --query value -o tsv`
- **Token scope**: `read:packages` on `ghcr.io`
- **Acceptance**: `infra.yml` with `deployContainerApps=true` succeeds without manually providing `ghcrToken` input.

### B-081 Details (Review and Merge Dependabot PRs)
- **Problem**: ~20 Dependabot PRs opened automatically after P6-006 enabled Dependabot. These cover NuGet, npm, and GitHub Actions dependency updates.
- **Approach**: Review each PR for breaking changes, run CI, merge in batches. Some may have conflicts if they touch the same lock files.
- **Risk**: Major version bumps (e.g., Aspire 9.x → 13.x) may require code changes. Minor/patch bumps are usually safe.
- **Acceptance**: All Dependabot PRs merged or closed with justification. CI green on develop after merging.

### B-075 Details (CRLF Line Ending Fix)
- **Problem**: Several files in the repo were committed with CRLF line endings while `.gitattributes` specifies `eol=lf`. This causes phantom diffs that persist through `git reset --hard`, `git stash`, and `git checkout`, making branch switches and merges difficult.
- **Affected files**: `infra/main.parameters.prod.json`, `infra/modules/sql.bicep`, `.github/workflows/infra.yml`, and ~12 others (mostly infra/ and config files).
- **Fix**: Run `git add --renormalize .` on develop to convert all tracked files to LF in the index, then commit. This is a one-time normalization.
- **Impact**: Pure whitespace change, no functional impact. Will show large diffs on affected files.

### B-076 Details (SQL Connection String Sync — Done)
- **Problem**: Pushing `infra/` changes to main auto-triggers `infra.yml`, which runs a Bicep deploy that resets the SQL server admin password to the Key Vault value. But the container app's connection string env var still has the old password → Error 18456 → HTTP 500 on all endpoints.
- **Root cause**: SQL server password and container connection string are set independently. Bicep deploy changes the server password but doesn't update the container (when `deployContainerApps=false`).
- **Fix**: Added "Sync SQL connection string to Container Apps" step in `infra.yml` that runs after every Bicep deploy. It reads the Key Vault password, builds the connection string, and updates the container app's env var if the container exists.
- **Manual fix applied**: Updated dev container's `ConnectionStrings__sessionsight` via `az containerapp update --set-env-vars` to match Key Vault password.

### B-077 Details (Managed Identity for SQL Auth)
- **Problem**: SQL auth uses password-based login (`sessionsightadmin`), requiring password sync between Key Vault, SQL server, and container connection strings. Password drift causes outages (see B-076).
- **Fix**: Switch to Azure AD / Managed Identity authentication for SQL. The container app's system-assigned managed identity would authenticate directly — no passwords in connection strings.
- **Priority**: Low — B-076 sync step mitigates the immediate issue. This is a cleaner long-term solution.
- **Scope**: Update `infra/main.bicep` (SQL AAD admin), `infra/modules/containerApps.bicep` (connection string without password), and EF migrations connection string in `deploy.yml`.

### P6-007 Details (Demo Data and Walkthrough - Stage)
- **Related**: B-072 covers the same seeding issue for dev environment.
- **Scope**: Seed stage database with demo data and create walkthrough documentation.
- **Note**: Implementation approach from B-072 (EF seed migration) auto-applies when EF migrations run on stage DB.

### B-069 Details (Extraction Timeout Investigation)
- Context: Case risk-test-034 hit a 300s `HttpClient.Timeout` during golden E2E (first run). Passed on second run.
- Investigate: Check if golden tests use `fixture.LongClient` (5-min timeout) or regular client. May need to extend timeout for extraction-heavy golden cases.
- Rate limit mitigation already in place: Retry base delay increased from 1s to 3s (~93s total window) via `SpacedRetryPolicy`.

### P5-001 / B-038 Investigation Notes (2026-02-10)
- Harness file: `tests/SessionSight.FunctionalTests/GoldenExtractionTests.cs` is currently marked `[Theory(Skip = ...)]` while strict v2 expectation tuning continues.
- Current contract: v2 risk files use stage-aware expectations (`expected_by_stage`) with top-level `assert_stages` and `assert_fields`.
- Diagnostics now emitted in test output and persisted to run-level extraction columns plus `RiskDecisionsJson` (per-field decisions with `criteria_used` + `reasoning_used`).
- Targeted 5 active files for stabilization:
  - `risk-test-001_v2.json`
  - `risk-test-007_v2.json`
  - `risk-test-015_v2.json`
  - `risk-test-025_v2.json`
  - `risk-test-033_v2.json`
- Run only these tests while iterating:
  - `./scripts/run-e2e.sh --filter "GoldenExtractionTests"`
- Optional deterministic replay controls:
  - `GOLDEN_DATE=2026-02-08 ./scripts/run-e2e.sh --filter "GoldenExtractionTests"`
  - `GOLDEN_MODE=full ./scripts/run-e2e.sh --filter "GoldenExtractionTests"`
- Optional targeted subset control:
  - `GOLDEN_FILTER=risk-test-025 ./scripts/run-e2e.sh --filter "GoldenExtractionTests"`
- Selection boundary is now 7:00 AM Eastern; before 7:00 AM ET, operational day uses prior date.
- Preview artifacts are refreshed each run and kept at exactly 5 files in `/tmp/sessionsight/golden-previews/`.
- Latest strict targeted results before temporary skip (`GOLDEN_MODE=full` with per-case `GOLDEN_FILTER`):
  - `risk-test-001_v2`: FAIL (`risk_reextracted.risk_level_overall` expected `High`, got `Low`)
  - `risk-test-007_v2`: PASS
  - `risk-test-015_v2`: FAIL (`clinical_extractor.si_frequency` expected `Frequent`, got `Rare`)
  - `risk-test-025_v2`: FAIL (`risk_final.si_frequency` expected `Occasional`, got `Rare`)
  - `risk-test-033_v2`: FAIL (`clinical_extractor.suicidal_ideation` expected `ActiveWithPlan`, got `ActiveNoPlan`; re-extracted/final were `ActiveWithPlan`)
- Diagnostics schema cleanup (2026-02-10):
  - Renamed `CriteriaValidationAttemptsUsed` → `CriteriaValidationAttempts`, `RiskDecisionsJson` → `RiskFieldDecisionsJson`.
  - Added `GuardrailApplied` (summary bool) and `DiscrepancyCount` (stage drift counter) as queryable columns.
  - Widened guardrail reason columns from 100→200 chars.
  - API DTO restructured: 6 flat diagnostic params replaced with typed `RiskDiagnosticsDto` containing `GuardrailDetailDto` and `RiskFieldDecisionDto`.
  - Mapping layer deserializes `RiskFieldDecisionsJson` into typed DTOs; returns `null` when no diagnostics data exists.
  - Zero overlap: columns store summary/guardrail scalars, JSON stores only the per-field decision audit trail.
- Non-golden functional stability update:
  - `ExtractionAssertions` was adjusted for clinically valid dual phrasing of presenting concern duration (`ongoing` vs `past two weeks`) when both appear in the same note.
  - After this fix, `./scripts/run-e2e.sh --all` passed (backend functional: 8 passed, 1 skipped; frontend full-stack Playwright: 3 passed, 1 skipped).

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
| B-046 | Add local API file logging (Serilog) to `/tmp/sessionsight/` + update debug docs/scripts | 2026-02-09 |
| B-066 | Remove temporary DIAG_LOG hack (`/tmp/api-diag.log`) and legacy docs/scripts after Serilog validation | 2026-02-09 |
| B-068 | Add prompt rule: infer si_frequency from severity when evidence absent | 2026-02-10 |
| B-069 | Increase LongClient timeout from 5 to 7 minutes for extraction pipeline | 2026-02-10 |
| B-038 | Golden files for non-risk extraction fields (5 cases, 8 sections) | 2026-02-11 |
| P5-001 | Integration tests (golden files: 20 risk + 5 non-risk cases, tests un-skipped) | 2026-02-11 |
| B-017 | Safety/red-team evals (14 adversarial golden files, 6 categories, 0 injection successes) | 2026-02-11 |
| B-070 | Merge redundant E2E extraction tests into shared collection fixture | 2026-02-11 |
| B-071 | Prompt hardening: euphemistic language → active SI classification | 2026-02-11 |
| P6-003 | GitHub Actions deploy.yml (full CI/CD pipeline) | 2026-02-12 |
| B-067 | Cloud logging validation + troubleshooting playbook | 2026-02-13 |
| B-072 | Cloud database seeding + Therapist CRUD + ProcessingJob status screen | 2026-02-13 |
| P6-002 | Configure stage environment (pre-production Azure resources) | 2026-02-14 |
| B-073 | Add `deployContainerApps`/`ghcrToken` inputs to infra.yml workflow | 2026-02-14 |
| B-074 | Automate EF migrations in deploy.yml (run after image update) | 2026-02-14 |
| B-076 | Sync SQL connection string after infra deploy | 2026-02-14 |
| B-075 | Fix CRLF line endings (renormalize to LF) | 2026-02-14 |
| B-030 | Promotion model: dev→stage approval rules (branch protection + env gates + deploy.yml split) | 2026-02-14 |
| P6-004 | Environment-specific configuration (Key Vault, ASPNETCORE_ENVIRONMENT, config files) | 2026-02-14 |
| B-032 | Document size validation (pre-upload size check + DocumentValidationException) | 2026-02-16 |
| B-064 | Extraction trigger race condition fix (atomic TryTransitionDocumentStatusAsync) | 2026-02-16 |
| B-034 | Patient idempotency race condition fix (GetOrCreateByExternalIdAsync with retry) | 2026-02-16 |
| B-048 | Circuit breaker for Azure SDK clients (CircuitBreakerState + HttpPipelinePolicy + RetryPolicy) | 2026-02-16 |
| P6-005 | GitHub Release tag trigger (v* tags in deploy.yml) | 2026-02-17 |
| B-029 | Infra drift checks: ARM validate (dev + stage) + stage what-if in PR preview | 2026-02-17 |
| B-031 | Rollback strategy: rollback_tag input, rollback job, runbook in CLOUD_TROUBLESHOOTING.md | 2026-02-17 |
| P6-006 | Enable Dependabot for dependency updates (NuGet, npm, GitHub Actions) | 2026-02-17 |
| B-077 | Switch to Managed Identity for SQL auth (eliminate password sync) | 2026-02-17 |
| B-080 | Store ghcrToken as GitHub secret (eliminate manual input for deployContainerApps) | 2026-02-18 |
| B-081 | Review and merge Dependabot PRs — 18 merged, 3 closed (eslint 10, Storage.Blobs breaking) | 2026-02-18 |
| B-082 | Fix BlobNotFound + stuck Processing + file types + sample documents on Upload page | 2026-02-18 |
| B-083 | Bump Azure OpenAI TPM, decouple extraction from HTTP lifecycle, fix retry UI, enable /health | 2026-02-18 |
| B-085 | Q&A Chat UI (patient-scoped clinical Q&A page with chat history, source citations) | 2026-02-19 |
| B-087 | Top Interventions horizontal bar chart card on Dashboard | 2026-02-20 |
| B-088 | Session summary regeneration button on SessionDetail | 2026-02-20 |
| B-089 | Delete/replace uploaded document (backend DELETE endpoint + frontend button) | 2026-02-20 |
| B-093 | Compare sessions tool for QA agent (side-by-side session comparison) | 2026-02-20 |
| B-086 | Patient longitudinal summary on timeline page | 2026-02-20 |

---

## Session Log (Last 5)

| Date | What Happened |
|------|---------------|
| 2026-02-20 | **B-086 complete: Patient longitudinal summary on timeline page.** Frontend-only change — `GET /api/summary/patient/{id}` already existed but was never called. Added `PatientSummary` + `GoalProgress` types to `types/index.ts`, `getPatientSummary()` API function in `api/summary.ts`, `usePatientSummary` query hook, and summary card panel on `PatientTimeline.tsx` between stats bar and session list. Panel shows progress narrative, mood trend badge, effective interventions, recurring themes, goal progress, risk trend summary, and recommended focus. Loading spinner during fetch, hidden on 404 (patients with no extraction data). Tests: 202 frontend unit (7 new: 3 hook, 2 API, 2 page), 17 Playwright smoke (patient summary route mock added). |
| 2026-02-20 | **B-087/088/089/093 complete: 4 quick wins from gap audit.** B-087: Top Interventions horizontal bar chart card on Dashboard (frontend-only, renders `topInterventions[]` from existing `PracticeSummary` API). B-088: Session summary regenerate/generate button on SessionDetail — new `api/sessionSummary.ts`, `useRegenerateSessionSummary` hook, button shows "Generate Summary" when no summary exists. B-089: Full-stack delete document — backend `DELETE /api/sessions/{id}/document` (blob + search index + DB), frontend red "Delete Document" button with `window.confirm`, `useDeleteDocument` hook. B-093: `CompareSessionsTool` for QA agent — compares 2+ sessions across mood, risk, interventions with change summary; registered in DI, added to agentic loop with `AllowedPatientId` guard, prompt updated. Also fixed `start-dev.sh` missing venv PATH export (caused `az` not found → LLM endpoints hang) and added `az login` warning to both start scripts. Tests: 726 backend (including 4 CompareSessionsTool + 3 DocumentsController delete), 195 frontend (including 7 new hook/page tests), 17 Playwright smoke (2 new assertions). PR #76. |
| 2026-02-19 | **B-085 complete + Gap audit: 9 new backlog items (B-085–B-093) + 3 stale blocker fixes + B-083 closed.** B-085: Q&A Chat UI page with patient selector, chat-style message history, source citations, loading states, clear button. PR #75 merged. Ran three audits: (1) Backend capabilities with no frontend consumer — found Q&A Chat UI, patient summary, practice breakdown, session regen, delete/replace doc. (2) Specs vs backlog — found missing tickets for doc validation review-routing, RAG eval harness, SLO measurement; stale blockers on P6-007/P5-003/B-015. (3) Implementation vs design — confirmed summarizer tools gap (2 of 3 already exist in `AggregateMetricsTool`, only `compare_sessions` is new); document validation review-routing signals already in Azure SDK response but discarded. Fixed stale blockers: P6-007 Ready (P6-002 done), P5-003 Ready (P1-019 done), B-015 Ready (P1-004 done). Marked B-083 Done (commits d899432, d55d466, ca108cb). Added note to B-035 re: dependency on B-085 for user visibility. |
| 2026-02-18 | **B-082 complete: Fix BlobNotFound + stuck Processing + file types + sample documents.** Fixed 3 production bugs: (1) URL-decode blob path in `AzureBlobDocumentStorage` — filenames with spaces/parens caused BlobNotFound on extraction. (2) Replaced `UpdateDocumentStatusAsync` with `TryTransitionDocumentStatusAsync` in all 3 `ExtractionOrchestrator` failure paths — change-tracker staleness caused status stuck at Processing. (3) Removed `.txt` from frontend accept list, added backend extension allowlist (`.pdf,.docx,.doc,.jpg,.jpeg,.png,.tiff,.bmp`) with 400 BadRequest for unsupported types. Added sample documents feature: generated 8 static therapy note PDFs (5 non-risk from golden files, 3 risk notes expanded to full structured format) via `fpdf2` script. Built sample document picker on Upload page with tab toggle (Sample Documents / Your Document), card grid with preview and "Use This" buttons. New test project: `SessionSight.Infrastructure.Tests` with 8 blob path round-trip tests. Added 4 Playwright smoke tests for Upload page sample UI. Updated 3 orchestrator tests, 1 E2E test (tab click). Validation: 724 backend tests pass (83.35% coverage), frontend 5/5 gates pass (15 smoke tests), 0 warnings. |
| 2026-02-17 | **P6-006/B-077 complete: Dependabot + Managed Identity for SQL.** P6-006: Created `.github/dependabot.yml` with three ecosystems (NuGet, npm, GitHub Actions) for automated dependency update PRs. B-077: Eliminated password-based SQL auth — added AAD admin to `sql.bicep`, removed `sqlAdminPassword` param from `main.bicep` (uses generated throwaway for server creation), switched connection string to `Authentication=Active Directory Managed Identity`, removed `@secure()` and secret ref from `containerApps.bicep` (plain env var), replaced Key Vault password fetch with `Active Directory Default` in `deploy.yml` EF migrations, replaced password sync step with MI user provisioning (T-SQL `CREATE USER FROM EXTERNAL PROVIDER`) in `infra.yml`, removed `sqlAdminPassword` from parameter files and all ARM validate/what-if commands. Updated `CLOUD_TROUBLESHOOTING.md` (MI-specific troubleshooting, removed SQL Password Sync section) and `CLAUDE.md` (passwordless Deploy Bicep command). |
| 2026-02-17 | **P6-005/B-029/B-031 complete: CI/CD hardening.** P6-005: Added `tags: ['v*']` to `deploy.yml` push trigger so GitHub releases auto-deploy. B-029: Added ARM-level validation (`az deployment sub validate`) for both dev and stage parameter files to `infra.yml` validate job; added stage what-if to PR preview job with dual-environment PR comment. B-031: Added `rollback_tag` workflow_dispatch input to `deploy.yml`, guarded build/deploy jobs to skip on rollback, added dedicated `rollback` job that updates container images without building, added rollback runbook to `docs/CLOUD_TROUBLESHOOTING.md`. Updated `.claude/CLAUDE.md` with Releases & Deployment section. |
| 2026-02-16 | **B-032/B-064/B-034/B-048 complete: Four functional fixes.** B-032: Added `DocumentValidationException`, pre-upload size/empty checks in `DocumentsController`, changed parser to throw `DocumentValidationException` instead of `InvalidOperationException`. B-064: Added `TryTransitionDocumentStatusAsync` (atomic `ExecuteUpdateAsync` with WHERE clause) to `SessionRepository`, used in `ExtractionController` and `ExtractionOrchestrator` to prevent concurrent extraction. Supports both Pending→Processing and Failed→Processing (retry). B-034: Added `GetOrCreateByExternalIdAsync` to `PatientRepository` with catch-and-retry on unique constraint violation, used in `IngestionController`. B-048: Created `CircuitBreakerState` (thread-safe state machine), `CircuitBreakerRegistry` (named singletons), `CircuitBreakerHttpPipelinePolicy` (Azure.Core), `CircuitBreakerRetryPolicy` (System.ClientModel), `CircuitBreakerOpenException` (→503). Wired into all 3 Azure SDK clients (OpenAI, Search, DocIntel) via new `AzureRetryDefaults` overloads. Config: 5 failures in 30s → open for 60s → half-open. Tests: 700+ passing, 83.46% coverage. |
| 2026-02-14 | **P6-004 post-deploy verification complete.** All 6 checks passed. Health: both APIs responding (no `/health` endpoint mapped — pre-existing, not a regression — but `/api/patients` returns 200 on both). Scalar: dev 200, stage 404. CORS: dev returns `Access-Control-Allow-Origin: http://localhost:5173` on preflight, stage returns 405 with no CORS headers (middleware not active in Production). Environment identity: `az containerapp show` confirms dev=`Staging`, stage=`Production`. Functional: dev GET patients/therapists 200, POST+DELETE patient 201/204; stage GET patients/therapists 200. P6-004 fully closed. |
| 2026-02-14 | **P6-004 complete: Environment-specific configuration.** Fixed stage deploy.yml Key Vault reference (`sessionsight-kv-dev` → `sessionsight-kv-stage`). Parameterized `ASPNETCORE_ENVIRONMENT` in Bicep (dev=`Staging`, stage=`Production`). Widened Swagger/CORS gate to include `IsStaging()`. Created `appsettings.Staging.json` (cloud dev: Information logging, request logging on) and `appsettings.Production.json` (cloud stage: Warning logging, request logging off). Added `.gitignore` exceptions for new config files. Seeded `sql-admin-password` secret into `sessionsight-kv-stage` + granted developer RBAC on stage KV. Deployed infra (both envs with `deployContainerApps=true`) and app images (both envs). Verified: dev container `ASPNETCORE_ENVIRONMENT=Staging`, stage container `ASPNETCORE_ENVIRONMENT=Production`. Also added `git fetch origin develop` to CLAUDE.md git workflow to prevent stale-branch conflicts. Remaining: post-deploy verification (Swagger, CORS, logging behavior). |
| 2026-02-14 | **P6-002 complete: Stage environment fully deployed.** Completed B-073 (PR #7: `deployContainerApps`/`ghcrToken` inputs to `infra.yml`) and B-074 (PR #7+#9: EF migrations in `deploy.yml` with `dotnet restore` fix). Merged develop→main (PRs #8, #10) triggering auto-deploy. Dev deploy: images built, containers updated, EF migrations passed (run 22011841249). Stage deploy: manual dispatch succeeded — images, containers, EF migrations all green. **Stage verified**: `/api/therapists` returns seeded data, `/api/patients` returns 200, web returns 200. **Dev 500 fix (B-076)**: `infra.yml` auto-triggered on push to main (infra/ changes), Bicep reset SQL server password to Key Vault value but container still had old password → Error 18456. Fixed manually via `az containerapp update --set-env-vars`. Added permanent fix: `infra.yml` now syncs the container connection string after every Bicep deploy. Both dev and stage verified healthy. Filed B-075 (CRLF), B-077 (managed identity for SQL — low priority). |
| 2026-02-14 | **P6-002 stage infra deployed, app running on stale images.** Merged PR #6 (Bicep code). Set up GitHub `stage` environment + OIDC credential + secrets via CLI. Ran `infra.yml` what-if and deploy for stage — created KV (`sessionsight-kv-stage`), storage (`sessionsightstoragestage`), SQL DB (`sessionsight-stage`). Deployed Container Apps via manual `az deployment sub create` with `deployContainerApps=true` (not yet in workflow inputs — filed B-073). Ran EF migrations manually on stage DB (not yet automated — filed B-074). Search index `sessionsight-sessions-stage` created after RBAC propagation delay + container restart. Stage API and Web running, `/api/patients` returns 200. Problem: `main` is 6 commits behind `develop` — container images are pre-B-072, so `/api/therapists` returns 404. Next: merge develop→main, trigger deploy to stage. |
| 2026-02-20 | **P6-007 complete: Demo data seeding.** Updated EF therapist seed data from "Default Therapist" to "Dr. Sarah Mitchell" (PhD, LPC, license LPC-2024-0847) with auto-generated UpdateData migration. Overhauled `start-dev.sh`: removed dead SQL INSERT step (EF migration handles it), replaced 2-patient seed with conditional 8-patient full-extraction pipeline. Guard: `GET /api/patients` — skips if >= 3 patients exist (fresh DB = 0 seeds, old seed = 2 re-seeds, already seeded = 8 skips). Creates 8 patients matching the existing sample PDFs (Sarah Chen/anxiety, Marcus Williams/depression, Elena Rodriguez/PTSD, David Thompson/substance use, Jennifer Walsh/termination, Rachel Morrison/active SI, Harold Jacobson/elderly grief, Brian Okafor/intake eval). Sequential create+upload (~10s), then parallel extraction (~5-8 min, ~$0.24 one-time). Updated startup banner with therapist name and all 8 demo patient descriptions. |
| 2026-02-13 | **B-072 complete: Therapist CRUD + ProcessingJob status + EF seeding.** Added EF migration `SeedDefaultTherapist` to solve B-072 FK constraint issue. Built full Therapist CRUD: backend (repo, controller, DTOs, validators, tests) + frontend (`/therapists` page, create form, API client, hooks, 5 unit tests, smoke tests). Built ProcessingJob read-only status screen: backend (`GET /api/processing-jobs`) + frontend (`/jobs` page with 5s auto-refresh polling when active jobs exist, fixtures, tests). Replaced hardcoded `DEFAULT_THERAPIST_ID` in Sessions.tsx with therapist dropdown fetching from API. Added 2 Playwright smoke tests, 1 full-stack E2E test, 7 backend functional tests (TherapistCrudTests), 15 backend unit tests, 10 frontend unit tests. Fixed 3 test failures: Processing Jobs strict mode (cell selector), Sessions route mocking (query params), TherapistCrudTests substring bug (`[..36]` on 35-char string). Validation: 700 backend tests pass (83.34% coverage), 173 frontend tests pass (87.9% coverage), all E2E/smoke tests pass. Files: 27 new, 13 modified. |

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
