# SessionSight Project Guide

## Validation Order (Default)

Run in this order to minimize wasted E2E runs:

1. `dotnet test --filter "Category!=Functional"`
2. `./scripts/check-frontend.sh`
3. Choose E2E scope:
   - Backend only: `./scripts/run-e2e.sh`
   - Frontend full-stack (browser -> API -> DB): `./scripts/run-e2e.sh --frontend`
   - Both backend + frontend: `./scripts/run-e2e.sh --all`

Before `git push`:
1. `dotnet build`
2. `./scripts/check-coverage.sh`
3. Re-run required E2E scope if code changed after checks

## Quick Start Commands

**"Run the real deal" (full stack for manual testing):**
1. `./scripts/start-aspire.sh` — starts backend (find API port with `ss -tlnp | grep SessionSight`)
2. `cd src/SessionSight.Web && services__api__https__0=https://localhost:<PORT> npx vite --host` — frontend at http://localhost:5173

**Playwright test modes (user):** `./scripts/watch-frontend-tests.sh` (UI mode) or `--headed` (visible browser)

**Playwright MCP (Claude):** Use `mcp__playwright__browser_*` tools — `browser_navigate`, `browser_snapshot`, `browser_click`, `browser_take_screenshot`, `browser_console_messages` for debugging

**After E2E tests:** Data is populated — refresh frontend to see real extractions in Review Queue

## Architecture Overview

**Pipeline:** Document → IntakeAgent → ClinicalExtractorAgent → RiskAssessorAgent → SummarizerAgent → EmbeddingService → SearchIndex → Database

**Agents:**
- `IntakeAgent` - Validates document is a therapy note
- `ClinicalExtractorAgent` - Extracts 82 fields using agent loop + tools
- `RiskAssessorAgent` - Safety validation of risk fields
- `SummarizerAgent` - Generates session/patient/practice summaries
- `QAAgent` - Dual-path Q&A: simple questions → single-shot RAG, complex → agentic loop with 4 tools

**Summary API:**
- `GET /api/summary/session/{id}` - Session summary (stored or regenerate)
- `GET /api/summary/patient/{id}` - Patient longitudinal summary
- `GET /api/summary/practice?startDate=&endDate=` - Practice metrics

**Q&A API:**
- `POST /api/qa/patient/{patientId}` - Ask clinical question about a patient (body: `{"question": "..."}`)
  - Simple questions → single-shot RAG (fast, gpt-4o-mini)
  - Complex questions → agentic loop with tools (gpt-4o), response includes `toolCallCount`

**Q&A Agent Tools** (registered as concrete types, NOT as `IAgentTool`):
- `SearchSessionsTool` - Hybrid vector+keyword search over indexed sessions
- `GetSessionDetailTool` - Retrieve full extraction data for a single session
- `GetPatientTimelineTool` - Chronological timeline with risk/mood change detection
- `AggregateMetricsTool` - Compute metrics: mood_trend, session_count, intervention_frequency, risk_distribution, diagnosis_history

## Testing

```bash
# Unit tests (fast, no external dependencies)
dotnet test --filter "Category!=Functional"

# Backend E2E tests (requires Azure services)
./scripts/run-e2e.sh

# E2E hot mode (keeps Aspire running between runs)
./scripts/run-e2e.sh --hot

# E2E with test filter
./scripts/run-e2e.sh --filter TestName

# Frontend E2E (browser + real backend, costs LLM tokens)
./scripts/run-e2e.sh --frontend                     # Full-stack Playwright tests
./scripts/run-e2e.sh --frontend --headed            # Watch tests in visible browser
./scripts/run-e2e.sh --frontend --hot               # Reuse running Aspire (faster iteration)
./scripts/run-e2e.sh --frontend --hot --headed      # Combine flags
./scripts/run-e2e.sh --frontend --filter "upload"   # Run specific test by name

# Run both backend and frontend E2E
./scripts/run-e2e.sh --all                          # Backend C# + Playwright tests
```

**Frontend E2E notes (`--frontend`):**
- **Cost:** ~$0.05-0.10 per run (LLM extraction uses GPT-4o). Run sparingly.
- **Duration:** ~2 minutes (extraction takes ~1.5 min)
- **What it tests:** Create patient → Create session → Upload PDF → Wait for extraction → Verify extraction results → Verify session status
- **Debug failures:** Screenshots saved to `src/SessionSight.Web/test-results/`
- **Test PDF:** Uses `tests/SessionSight.FunctionalTests/TestData/sample-note.pdf`

## E2E Troubleshooting

**Before running expensive E2E:** Always run `dotnet test --filter "Category!=Functional"` and `./scripts/check-frontend.sh` first. These are fast and free, and catch issues before spending money on LLM calls.

**E2E runtime tips:**
- `[Codex Only]` Run `./scripts/run-e2e.sh ...` with elevated permissions in this environment. Non-elevated runs can fail on process/network/runtime capabilities and cause false startup timeouts.
- `--hot` is reuse-only today: it keeps services running only if Aspire is already running before script start. If Aspire is not running, the script falls back to normal mode and cleans up on exit.
- Add fail-fast preflight checks in `run-e2e.sh` for missing prerequisites (permissions, Docker, AppHost startup) so failures stop immediately instead of timing out after 120s.
- `./scripts/start-aspire.sh` starts the stack without seeding test data.
- For seeded demo data with a running stack: start Aspire first, then run a targeted hot flow, e.g. `./scripts/run-e2e.sh --frontend --hot --filter "complete patient -> session -> upload -> review flow"`.

**Diagnosing failures:** On the FIRST run, grep the output for errors instead of re-running:
```bash
./scripts/run-e2e.sh 2>&1 | tee /tmp/e2e-output.log
# Then inspect:
grep -E "FAIL\]|Error Message:" /tmp/e2e-output.log
```

**Docker network exhaustion:** If Aspire fails to start with "all predefined address pools have been fully subnetted", orphaned networks have accumulated. The cleanup function in run-e2e.sh removes orphaned `sql-*` and `storage-*` containers first, then prunes networks. If it still fails, run manually:
```bash
docker ps -a --format '{{.Names}}' | grep -E 'sql-|storage-' | xargs -r docker rm -f
docker network ls --filter "name=aspire" -q | xargs -r docker network rm
```
**Key:** Networks can't be removed while containers are attached. Remove containers FIRST (both `sql-*` AND `storage-*` names), then networks.

**Logs:** `/tmp/aspire-e2e.log` (view live: `tail -f /tmp/aspire-e2e.log`)

**Common issues:**
- "No backend service" → Check Bicep deployment for AI Foundry connection
- "401/credential" errors → Run `az login`, verify Cognitive Services User role
- Port conflicts → `pkill -f SessionSight`
- "403 Forbidden" on search → Deploy Bicep with `developerUserObjectId` parameter:
  ```bash
  USER_ID=$(az ad signed-in-user show --query id -o tsv)
  az deployment sub create --location eastus2 --template-file infra/main.bicep \
    --parameters environmentName=dev sqlAdminPassword=<pwd> developerUserObjectId=$USER_ID
  ```
- Search indexing not working → Set user secret:
  ```bash
  cd src/SessionSight.Api
  dotnet user-secrets set "AzureSearch:Endpoint" "https://sessionsight-search-dev.search.windows.net"
  ```

## Agent Tool Pattern

**AgentLoopRunner** has two `RunAsync` overloads:
- `RunAsync(chatClient, messages, ct)` → uses DI-injected `IAgentTool` collection (extraction tools)
- `RunAsync(chatClient, messages, tools, ct)` → uses explicit tool list (Q&A tools)

When creating new `IAgentTool` implementations:
- Use `PropertyNameCaseInsensitive = true` for JSON deserialization
- Return `ToolResult.Error()` for invalid inputs (don't throw)
- Singleton for stateless, Scoped if depends on repositories
- Tests: `tests/SessionSight.Agents.Tests/Tools/`

**Schema note:** Fields use `ExtractedField<T>` with Value, Confidence (0-1), Source

## Key Paths

| Path | Purpose |
|------|---------|
| `src/SessionSight.Agents/Tools/` | IAgentTool implementations |
| `src/SessionSight.Agents/Prompts/` | LLM prompts |
| `src/SessionSight.Api/Program.cs` | DI registration |
| `tests/SessionSight.FunctionalTests/` | E2E tests |

## Test Structure

| # | Type | Path | Framework | Coverage | Run Command | When To Run |
|---|------|------|-----------|----------|-------------|-------------|
| 1 | Backend Core Unit | `tests/SessionSight.Core.Tests/` | xUnit | 82% | `dotnet test --filter "Category!=Functional"` | Always |
| 2 | Backend Agents Unit | `tests/SessionSight.Agents.Tests/` | xUnit | 82% | (same) | Always |
| 3 | Backend API Unit | `tests/SessionSight.Api.Tests/` | xUnit | 82% | (same) | Always |
| 4 | Backend Functional | `tests/SessionSight.FunctionalTests/` | xUnit | n/a | `./scripts/run-e2e.sh` | Backend/agent pipeline changes |
| 5 | Frontend API Unit | `src/SessionSight.Web/__tests__/api/` | Vitest+MSW | n/a | `npx vitest run` | Frontend API contract changes |
| 6 | Frontend Hooks Unit | `src/SessionSight.Web/__tests__/hooks/` | Vitest+MSW | n/a | (same) | Frontend data-flow changes |
| 7 | Frontend Pages Unit | `src/SessionSight.Web/__tests__/pages/` | Vitest+MSW | n/a | (same) | UI/page behavior changes |
| 8 | Frontend Components | `src/SessionSight.Web/__tests__/components/` | Vitest | n/a | (same) | Component logic/props changes |
| 9 | Frontend Smoke E2E | `src/SessionSight.Web/e2e/smoke.spec.ts` | Playwright | n/a | `npx playwright test --project=chromium` | Frontend route and rendering sanity |
| 10 | Full-Stack E2E | `src/SessionSight.Web/e2e/full-stack/` | Playwright | n/a | `./scripts/run-e2e.sh --frontend` | Real browser -> API -> DB validation |

**Frontend test conventions:**
- Tests live in `__tests__/` (NOT `src/__tests__/`) — vitest.config.ts pattern is `__tests__/**/*.test.{ts,tsx}`
- Imports use `../../src/` prefix (e.g., `import { X } from '../../src/api/x'`)
- Use shared MSW server from `../../src/test/mocks/server` — don't create standalone servers
- Add fixtures to `src/test/fixtures/` and handlers to `src/test/mocks/handlers.ts`
- Labels need `htmlFor` + `id` for accessibility tests (`screen.getByLabelText()`)

**Backend test conventions:**
- Controller tests mock repositories
- Integration tests use `WebApplicationFactory<Program>`
- Functional tests require Azure services (run with `./scripts/run-e2e.sh`)

## Lessons Learned (Common Pitfalls)

### Code Analysis Rules (Sonar/CA)
- **CA1848**: Must use `[LoggerMessage]` delegates, NOT `_logger.LogWarning()` directly
- **S6966**: Must use async file operations (`File.AppendAllTextAsync`), NOT sync versions
- Always run `dotnet build` after adding debug code to verify it compiles

### Infrastructure Changes
- **All Azure changes go in Bicep** - don't run `az role assignment create` directly
- After modifying `infra/*.bicep`, deploy with:
  ```bash
  USER_ID=$(az ad signed-in-user show --query id -o tsv)
  SQL_PWD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')
  az deployment sub create --location eastus2 --template-file infra/main.bicep \
    --parameters environmentName=dev sqlAdminPassword="$SQL_PWD" developerUserObjectId=$USER_ID
  ```
- Azure AI Search needs `aadOrApiKey` auth (not `apiKeyOnly`) for RBAC to work

### Aspire Configuration
- **User secrets alone aren't enough** for Aspire-hosted projects
- Must add config to AppHost: `.WithEnvironment("Section__Key", "value")`
- Use double underscore (`__`) for nested config in environment variables

### E2E Tests
- **Full-stack Playwright E2E catches type mismatches** — Unit tests with mocked data won't catch frontend/backend type drift. Example: `ExtractedField.source` was typed as `string` in frontend but backend returns `{text, startChar, endChar, section}`. Only full-stack E2E with real LLM data revealed this (React crashed with "Objects are not valid as React child").
- **E2E test classes run in parallel** — Azure SDK retry/backoff (B-010) handles rate limits from concurrent extractions
- **Extraction timeout**: `fixture.Client` has 120s timeout, `fixture.LongClient` has 5-min timeout — use `LongClient` for extraction calls
- **Transient retry**: ApiFixture wraps both clients with `RetryHandler` (single retry, 1s delay) for socket resets, TLS failures, and 502/503/504. Don't create raw `HttpClient` in tests — use the fixture
- **Retry on infrastructure signals, not LLM signals**: Azure AI Search indexing is near-real-time, not instant. E2E tests should retry on `sources.length > 0` (search found data), NOT on `confidence > 0` (LLM's subjective self-assessment which can be 0 even when pipeline worked correctly)
- **API logs not visible during headless E2E** - Aspire sends child process logs to OTLP/Dashboard (browser-only)
  - `/tmp/aspire-e2e.log` only captures AppHost output, NOT API project logs
  - **Workaround:** Add temp file logging: `await File.AppendAllTextAsync("/tmp/api-diag.log", msg)`
  - **Permanent fix:** B-046 (add Serilog file logging) or B-047 (replace Aspire with Docker Compose)

### Debugging Silent Failures
- SearchIndexService has graceful degradation - add logging when operations are skipped
- Add startup logging for configuration values (especially endpoints)
- Add timeouts to external API calls (embedding, search) to prevent indefinite hangs
- The orchestrator's try-catch swallows exceptions - check Warning-level logs

### Debugging LLM Pipeline Failures
- **Look at the actual LLM output FIRST.** Add diag logging before writing code fixes. Assumptions about what the LLM returns are usually wrong — one log line showing the real output saves hours of speculative coding.
- **Aspire env vars don't propagate from the shell.** `DIAG_LOG=1 ./scripts/run-e2e.sh` sets the var in the test runner, NOT the API child process. Use `.WithEnvironment("DIAG_LOG", "1")` in AppHost, or use unconditional `File.AppendAllTextAsync` for quick debugging.
- **E2E tests must assert data quality, not just success.** A test that checks `success == true` without verifying extracted fields is a false positive — the pipeline can "succeed" with all-empty data.
- **Use `--filter TestName` when iterating** on a single E2E test to avoid running all 8 tests each loop ($$$).
- **Generate schemas from source types, don't hardcode.** If a prompt needs the JSON schema, generate it via reflection (see `ExtractionSchemaGenerator`). Hardcoded schemas drift when fields are added.
- **Always set `ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()`** on ChatCompletionOptions for any agent that returns JSON. This guarantees valid JSON at the API level. Without it, the LLM can return prose, markdown fences, or broken JSON — especially after long agent loops. Keep the "CRITICAL: JSON only" prompt instruction as defense-in-depth.

### E2E Assertion Patterns for LLM-Extracted Fields
- **Deterministic fields** (explicitly stated in note: dates, scores, "None"): Assert exact values, but guard with `if (value != null && value != default)` for occasional LLM extraction failures.
- **Interpretive enum fields** (LLM chooses from enum): Validate against the FULL enum set (`BeOneOf(all valid values)`), NOT a subset. The LLM picks different values each run.
- **Free-text array fields** (keywords, themes, barriers): Use broad keyword stems (`"consist"` not `"consistent"`, `"appl"` not `"application"`), and use `ContainAny` with 6-8 stems.
- **FluentAssertions `ContainMatch` gotcha:** `|` is treated LITERALLY, not as OR. `ContainMatch("*a*|*b*")` looks for a literal pipe character. Use `Contain(sk => sk.Contains("a") || sk.Contains("b"))` instead.

### DiagLog: File-Based Debug Logging

**Problem:** Aspire sends API logs to OTLP/Dashboard (browser-only). During headless E2E tests, you can't see what's happening in the API.

**Solution:** `ExtractionOrchestrator.DiagLogAsync()` writes to `/tmp/api-diag.log` when enabled.

**How to enable:**
```bash
# In AppHost Program.cs (REQUIRED — shell env vars don't reach Aspire child processes):
.WithEnvironment("DIAG_LOG", "1")
```

**How to use in code:**
```csharp
// Add diagnostic logging at key points:
await ExtractionOrchestrator.DiagLogAsync($"Step 1: Downloading document from {uri}");
await ExtractionOrchestrator.DiagLogAsync($"Step 2 complete: IsValid={result.IsValid}");
await ExtractionOrchestrator.DiagLogAsync($"Error: {ex.Message}");
```

**How to view logs:**
```bash
# Watch live during E2E:
tail -f /tmp/api-diag.log

# Check after test:
cat /tmp/api-diag.log

# Clear before new run:
rm /tmp/api-diag.log
```

**Note:** This is a debugging workaround. For permanent fix, see B-046 (Serilog file logging).

### Validation Auto-Discovery
- FluentValidation validators are auto-discovered by `AddValidatorsFromAssemblyContaining<CreatePatientValidator>()` in Program.cs — no DI registration needed for new validators in the Api project

### Coverage Threshold
- Coverage hovers near the 82% threshold. Always write tests in the same pass as source code — don't do code-first-tests-later with repeated coverage checks. One pass: source file + test file together, then one build+coverage check at the end.

### Memory vs Plan Files
- All memories go in this CLAUDE.md, NOT in the auto memory file
- Backlog/session update procedure is canonical in `plan/docs/WORKFLOW.md` (Session End Checklist)
- Before marking backlog items Done, check spec docs: `plan/docs/specs/agent-tool-callbacks.md`, `phase-3-summarization-rag.md`, `PROJECT_PLAN.md`
- When simplifying scope, update backlog item name and create follow-up for deferred work
