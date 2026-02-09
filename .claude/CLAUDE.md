# SessionSight Project Guide

## Scripts Reference

| Script | Purpose | Options |
|--------|---------|---------|
| `start-dev.sh` | Full stack + migrations + sample data + frontend | (none) |
| `start-aspire.sh` | Backend only (no data, no frontend) | (none) |
| `run-e2e.sh` | Run E2E tests | `--frontend`, `--all`, `--hot`, `--headed`, `--filter "name"`, `--keep-db` |
| `check-frontend.sh` | Frontend validation (TS + Vitest + 82% coverage + Playwright smoke + build) | (none) |
| `check-coverage.sh` | Backend tests with 82% coverage check | `--report` |
| `watch-frontend-tests.sh` | Interactive Playwright UI | `--headed` |

**Endpoints (fixed ports):**
- Frontend: http://localhost:5173
- API: https://localhost:7039
- Dashboard: https://localhost:17055

---

## Running the App

```bash
# One command - full stack with sample data
./scripts/start-dev.sh

# Manual (if you need more control)
./scripts/start-aspire.sh  # Then in another terminal:
cd src/SessionSight.Web && services__api__https__0=https://localhost:7039 npx vite --host
```

**Playwright MCP (Claude):** Use `mcp__playwright__browser_*` tools — `browser_navigate`, `browser_snapshot`, `browser_click`, `browser_take_screenshot`, `browser_console_messages`

---

## Validation & Testing

**Validation order (minimize wasted E2E runs):**
1. `dotnet test --filter "Category!=Functional"` — unit tests (fast, free)
2. `./scripts/check-frontend.sh` — frontend validation (fast, free)
3. E2E scope (costs LLM tokens):
   - `./scripts/run-e2e.sh` — backend only
   - `./scripts/run-e2e.sh --frontend` — full-stack Playwright
   - `./scripts/run-e2e.sh --all` — both

**Before `git push`:**
1. `dotnet build`
2. `./scripts/check-coverage.sh` — backend 82% coverage
3. `./scripts/check-frontend.sh` — frontend 82% coverage

**Frontend E2E notes (`--frontend`):**
- **Cost:** ~$0.02-0.04 per run (LLM extraction uses gpt-4.1-mini/nano)
- **Duration:** ~2 minutes
- **Debug failures:** Screenshots in `src/SessionSight.Web/test-results/`
- **Test PDF:** `tests/SessionSight.FunctionalTests/TestData/sample-note.pdf`

---

## Architecture Overview

**Models:** gpt-4.1-mini (extraction, risk, complex Q&A), gpt-4.1-nano (intake, summarization, simple Q&A), text-embedding-3-large

**Pipeline:** Document → IntakeAgent → ClinicalExtractorAgent → RiskAssessorAgent → SummarizerAgent → EmbeddingService → SearchIndex → Database

**Agents:**
- `IntakeAgent` - Validates document is a therapy note
- `ClinicalExtractorAgent` - Extracts 82 fields using agent loop + tools
- `RiskAssessorAgent` - Safety validation of risk fields
- `SummarizerAgent` - Generates session/patient/practice summaries
- `QAAgent` - Dual-path Q&A: simple → single-shot RAG, complex → agentic loop

**APIs:**
- `GET /api/summary/session/{id}` - Session summary
- `GET /api/summary/patient/{id}` - Patient longitudinal summary
- `GET /api/summary/practice?startDate=&endDate=` - Practice metrics
- `POST /api/qa/patient/{patientId}` - Ask clinical question (body: `{"question": "..."}`)

**Q&A Agent Tools:**
- `SearchSessionsTool` - Hybrid vector+keyword search
- `GetSessionDetailTool` - Full extraction data for a session
- `GetPatientTimelineTool` - Chronological timeline with change detection
- `AggregateMetricsTool` - Compute metrics (mood_trend, session_count, etc.)

---

## Key Paths

| Path | Purpose |
|------|---------|
| `src/SessionSight.Agents/Tools/` | IAgentTool implementations |
| `src/SessionSight.Agents/Prompts/` | LLM prompts |
| `src/SessionSight.Agents/Routing/ModelRouter.cs` | Model selection (gpt-4.1-mini/nano) |
| `src/SessionSight.Api/Program.cs` | DI registration |
| `tests/SessionSight.FunctionalTests/` | E2E tests |

---

## Test Structure

| Type | Path | Run Command |
|------|------|-------------|
| Backend Unit | `tests/SessionSight.*.Tests/` | `dotnet test --filter "Category!=Functional"` |
| Backend E2E | `tests/SessionSight.FunctionalTests/` | `./scripts/run-e2e.sh` |
| Frontend Unit | `src/SessionSight.Web/__tests__/` | `npx vitest run --coverage` |
| Frontend Smoke | `src/SessionSight.Web/e2e/smoke.spec.ts` | `npx playwright test --project=chromium` |
| Full-Stack E2E | `src/SessionSight.Web/e2e/full-stack/` | `./scripts/run-e2e.sh --frontend` |

**Frontend test conventions:**
- Tests in `__tests__/` directory
- Use shared MSW server from `src/test/mocks/server`
- Labels need `htmlFor` + `id` for accessibility tests

**Backend test conventions:**
- Controller tests mock repositories
- Integration tests use `WebApplicationFactory<Program>`
- Functional tests require Azure services

---

## Troubleshooting

**Before running expensive E2E:** Always run unit tests + `check-frontend.sh` first.

**Diagnosing failures:**
```bash
./scripts/run-e2e.sh 2>&1 | tee /tmp/e2e-output.log
grep -E "FAIL\]|Error Message:" /tmp/e2e-output.log
```

**Common issues:**
| Issue | Fix |
|-------|-----|
| "401/credential" errors | `az login`, verify Cognitive Services User role |
| Port conflicts | `pkill -f SessionSight` |
| "403 Forbidden" on search | Deploy Bicep with `developerUserObjectId` parameter |
| Docker network exhaustion | Remove containers first: `docker ps -a --format '{{.Names}}' \| grep -E 'sql-\|storage-' \| xargs -r docker rm -f` |

**Log triage (first pass):**
```bash
curl -sk https://localhost:7039/health
tail -n 200 /tmp/sessionsight/aspire/aspire-e2e.log
tail -n 200 /tmp/sessionsight/vite/vite-e2e.log
ls -lah /tmp/sessionsight/
ls -lah /tmp/sessionsight/api/
tail -n 200 $(ls -1t /tmp/sessionsight/api/api-*.log 2>/dev/null | head -1)
```

**Extraction trace quick check:**
`LATEST=$(ls -1t /tmp/sessionsight/api/api-*.log | head -1); rg -n "HTTP POST /api/extraction|HTTP GET /api/sessions/.*/extraction|Extraction completed for session" "$LATEST" | tail -n 40`

**Request/response logging toggle (local):**
- Config section: `RequestResponseLogging` in `src/SessionSight.Api/appsettings.Development.json`
- Defaults: `Enabled=true`, `LogBodies=false`, `MaxBodyLogBytes=null`
- Temporary override:
  - `dotnet user-secrets set --project src/SessionSight.Api "RequestResponseLogging:LogBodies" "true"`
  - `dotnet user-secrets set --project src/SessionSight.Api "RequestResponseLogging:MaxBodyLogBytes" "4096"`
  - `dotnet user-secrets set --project src/SessionSight.Api "RequestResponseLogging:LogBodies" "false"`

**Deploy Bicep:**
```bash
USER_ID=$(az ad signed-in-user show --query id -o tsv)
SQL_PWD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')
az deployment sub create --location eastus2 --template-file infra/main.bicep \
  --parameters environmentName=dev sqlAdminPassword="$SQL_PWD" developerUserObjectId=$USER_ID
```

---

## Development Patterns

**AgentLoopRunner** has two `RunAsync` overloads:
- `RunAsync(chatClient, messages, ct)` → DI-injected tools (extraction)
- `RunAsync(chatClient, messages, tools, ct)` → explicit tool list (Q&A)

**IAgentTool implementations:**
- Use `PropertyNameCaseInsensitive = true` for JSON deserialization
- Return `ToolResult.Error()` for invalid inputs (don't throw)
- Singleton for stateless, Scoped if depends on repositories

**Schema:** Fields use `ExtractedField<T>` with Value, Confidence (0-1), Source

**Aspire config:** User secrets alone aren't enough — use `.WithEnvironment("Section__Key", "value")` in AppHost

**Validation:** FluentValidation validators are auto-discovered via `AddValidatorsFromAssemblyContaining<>()`

---

## Lessons Learned

### Code Analysis
- **CA1848**: Use `[LoggerMessage]` delegates, NOT `_logger.LogWarning()` directly
- **S6966**: Use async file operations, NOT sync versions

### SonarCloud
- **Local parity (.NET)**: `SonarAnalyzer.CSharp` in Directory.Build.props with `TreatWarningsAsErrors=true`
- **Local parity (Frontend)**: `eslint-plugin-sonarjs` catches ~30 rules (SonarCloud has 200+, no full parity exists)
- **NOSONAR comments don't work** for shell scripts or JSX — use CI exclusion filter instead
- **CI exclusions**: Edit `.github/workflows/ci.yml` EXCLUDED_RULES array for legitimate false positives
- **Suppress C# rules**: Use `#pragma warning disable SXXXX` with comment explaining why

### E2E Tests
- **Full-stack E2E catches type mismatches** — mocked unit tests won't catch frontend/backend type drift
- **Extraction timeout**: Use `fixture.LongClient` (5-min timeout) for extraction calls
- **Retry on infrastructure signals, not LLM signals**: Retry on `sources.length > 0`, NOT on `confidence > 0`
- **Cost control for flaky extraction assertions**: If `--all` fails one extraction-field assertion but logs show `POST /api/extraction/{id}` and `GET /api/sessions/{id}/extraction` both `200`, rerun only that test via `./scripts/run-e2e.sh --filter "TestName"` before rerunning full suite

### LLM Pipeline
- **Look at actual LLM output FIRST** — one log line saves hours of speculative coding
- **Aspire env vars don't propagate from shell** — use `.WithEnvironment()` in AppHost
- **Always set `ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()`** for JSON responses
- **Use `--filter TestName`** when iterating to avoid running all tests ($$$)
- **Determinism knobs (current defaults):** no `Seed`/`TopP`/penalties set; temperatures are Intake/Extractor/Risk `0.1`, QA answer `0.2`, QA complexity classifier `0.0`, Summarizer `0.3`

### Local API Logs
- Standard API log path: `/tmp/sessionsight/api/api-*.log`
- Keep body logging disabled unless actively debugging payload shape (`RequestResponseLogging:LogBodies=true`)
- If body logging is enabled, disable it again after targeted triage

### Coverage
- **82% threshold** for both backend and frontend — write tests with source code in same pass
- **E2E tests don't count** — Playwright runs in browser, can't measure code coverage
- Coverage reports: `coverage/` (frontend HTML), `coverage/report/` (backend)

### Memory
- All memories go in this CLAUDE.md, NOT auto memory file
- Backlog procedure: `plan/docs/WORKFLOW.md` (Session End Checklist)
