# SessionSight Agent Guide

Read this guide first when starting work in this repo.

Then use these downstream docs as needed:
- [`plan/docs/BACKLOG.md`](../plan/docs/BACKLOG.md) for current project state and next work
- [`docs/LOCAL_DEV.md`](LOCAL_DEV.md) for setup, secrets, ports, logs, and migrations
- [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) for pipeline behavior and system design
- [`plan/docs/WORKFLOW.md`](../plan/docs/WORKFLOW.md) for task-selection and backlog procedure
- [`docs/CLOUD_TROUBLESHOOTING.md`](CLOUD_TROUBLESHOOTING.md) for cloud operations and rollback details

## Working Preferences

- Do not create commits or push unless the user explicitly asks.

## MCP Usage

- Check MCP availability at session start (`list_mcp_resources`, `list_mcp_resource_templates`).
- If Playwright MCP is available, use it for browser-based verification and UI debugging.
- If MCP is unavailable, continue with shell/test-based workflows.

## LLM Test Guidelines

When fixing flaky or failing LLM/golden file tests, always try improving prompts first before widening tolerances or golden file values. Only widen after prompt improvements have been exhausted.

## Scripts Reference

| Script | Purpose | Options |
|--------|---------|---------|
| `start-dev.sh` | Full stack + migrations + sample data + frontend | (none) |
| `start-aspire.sh` | Backend only (no data, no frontend) | (none) |
| `run-e2e.sh` | Run E2E tests (flag required) | `--backend`, `--frontend`, `--all`, `--hot`, `--headed`, `--filter "name"`, `--keep-db` |
| `check-frontend.sh` | Frontend validation (TS + Vitest + 83% coverage + Playwright smoke + build) | (none) |
| `check-backend.sh` | Backend tests with 83% coverage check | `--report` |
| `watch-frontend-tests.sh` | Interactive Playwright UI | `--headed` |
| `load-test.sh` | k6 load tests (concurrent users) | `LOAD_TEST_EXPENSIVE=true` for LLM endpoints |

**Endpoints (fixed ports):**
- Frontend: http://localhost:5173
- API: https://localhost:7039
- Dashboard: https://localhost:17055

## Running the App

```bash
# One command - full stack with sample data
./scripts/start-dev.sh

# Manual (if you need more control)
./scripts/start-aspire.sh  # Then in another terminal:
cd src/SessionSight.Web && services__api__https__0=https://localhost:7039 npx vite --host
```

## Validation and Testing

**Validation order (minimize wasted E2E runs):**
1. `dotnet test --filter "Category!=Functional"` - unit tests
2. `./scripts/check-frontend.sh` - frontend validation
3. E2E scope:
   - `./scripts/run-e2e.sh --backend`
   - `./scripts/run-e2e.sh --frontend`
   - `./scripts/run-e2e.sh --all`

**Before pushing (validation):**
1. `dotnet build`
2. `./scripts/check-backend.sh`
3. `./scripts/check-frontend.sh`
4. `COVERAGE_THRESHOLD=0.80 COVERAGE_THRESHOLD_PERCENT=80 COVERAGE_FORMATS=opencover,cobertura ./scripts/check-backend.sh`

**Build and CI checks:**
- `gh pr checks <number> --watch`
- `gh run list --limit 5`
- `gh run watch <run-id> --exit-status`
- `gh run view <run-id> --log-failed`

**Frontend E2E notes (`--frontend`):**
- Cost: about `$0.01-0.02` per run
- Duration: about `2 minutes`
- Debug failures: screenshots in `src/SessionSight.Web/test-results/`
- Test PDF: `tests/SessionSight.FunctionalTests/TestData/sample-note.pdf`

## Git and Release Workflow

**Merge strategy:**
- `feature -> develop`: squash merge
- `develop -> main`: merge commit
- `main -> develop`: auto back-merge via `.github/workflows/back-merge.yml`

**Git workflow:**
1. Before code changes, create a fresh branch from latest `develop`.
2. Make changes.
3. Self-review with explicit verifications.
4. Update [`plan/docs/BACKLOG.md`](../plan/docs/BACKLOG.md) when task state changes.
5. Run local validation.
6. Push and create a PR.
7. Check CI and fix failures.
8. Wait for approval and merge.
9. Never push directly to `develop` or `main`.

**Releases and deployment:**
- Tags use the `v*` prefix and do not trigger deploys.
- Create releases via GitHub UI or `gh release create`.
- Deploys are triggered by merges to `main`.
- `deploy.yml` inputs:
  - `environment`: `dev` or `stage`
  - `rollback_tag`: 7-character SHA for rollback
- `infra.yml` inputs:
  - `environment`: `dev` or `stage`
  - `mode`: `deploy` or `what-if`
  - `deployContainerApps`: boolean
  - `ghcrToken`: PAT for `ghcr.io`

## Architecture Summary

**Models:** `gpt-4.1-mini` (extraction, risk, complex Q&A), `gpt-4.1-nano` (intake, summarization, simple Q&A), `text-embedding-3-large`

**Pipeline:** `Document -> IntakeAgent -> ClinicalExtractorAgent -> RiskAssessorAgent -> SummarizerAgent -> EmbeddingService -> SearchIndex -> Database`

**Agents:**
- `IntakeAgent` - validates document is a therapy note
- `ClinicalExtractorAgent` - extracts 82 fields using agent loop and tools
- `RiskAssessorAgent` - safety validation of risk fields
- `SummarizerAgent` - generates session, patient, and practice summaries
- `QAAgent` - dual-path Q&A: simple RAG or agentic loop

**APIs:**
- `GET /api/summary/session/{id}`
- `GET /api/summary/patient/{id}`
- `GET /api/summary/practice?startDate=&endDate=`
- `POST /api/qa/patient/{patientId}`

**Q&A tools:**
- `SearchSessionsTool`
- `GetSessionDetailTool`
- `GetPatientTimelineTool`
- `AggregateMetricsTool`

**Key paths:**
- `src/SessionSight.Agents/Tools/`
- `src/SessionSight.Agents/Prompts/`
- `src/SessionSight.Agents/Routing/ModelRouter.cs`
- `src/SessionSight.Api/Program.cs`
- `tests/SessionSight.FunctionalTests/`

## Test Structure

| Type | Path | Run Command |
|------|------|-------------|
| Backend Unit | `tests/SessionSight.*.Tests/` | `dotnet test --filter "Category!=Functional"` |
| Backend E2E | `tests/SessionSight.FunctionalTests/` | `./scripts/run-e2e.sh --backend` |
| Frontend Unit | `src/SessionSight.Web/__tests__/` | `npx vitest run --coverage` |
| Frontend Smoke | `src/SessionSight.Web/e2e/smoke.spec.ts` | `npx playwright test --project=chromium` |
| Full-Stack E2E | `src/SessionSight.Web/e2e/full-stack/` | `./scripts/run-e2e.sh --frontend` |
| Load Tests | `tests/load/smoke.js` | `./scripts/load-test.sh` |

**Frontend test conventions:**
- Tests live in `__tests__/`
- Use shared MSW server from `src/test/mocks/server`
- Labels need `htmlFor` + `id` for accessibility tests

**Backend test conventions:**
- Controller tests mock repositories
- Integration tests use `WebApplicationFactory<Program>`
- Functional tests require Azure services

## Troubleshooting and Patterns

**Before running expensive E2E:** always run unit tests and `check-frontend.sh` first.

**Diagnosing failures:**
```bash
./scripts/run-e2e.sh --backend 2>&1 | tee /tmp/e2e-output.log
grep -E "FAIL\\]|Error Message:" /tmp/e2e-output.log
```

**Common issues:**
- `401/credential` errors: run `az login` and verify Cognitive Services User role.
- Q&A or extraction hangs: Azure CLI not on API PATH.
- Port conflicts: `pkill -f SessionSight`
- Search `403`: deploy Bicep with `developerUserObjectId`
- Docker network exhaustion: remove stale containers

**Log triage (local):**
```bash
curl -sk https://localhost:7039/health
tail -n 200 /tmp/sessionsight/aspire/aspire-e2e.log
tail -n 200 /tmp/sessionsight/vite/vite-e2e.log
ls -lah /tmp/sessionsight/
ls -lah /tmp/sessionsight/api/
tail -n 200 $(ls -1t /tmp/sessionsight/api/api-*.log 2>/dev/null | head -1)
```

**Extraction trace quick check:**
```bash
LATEST=$(ls -1t /tmp/sessionsight/api/api-*.log | head -1)
rg -n "HTTP POST /api/extraction|HTTP GET /api/sessions/.*/extraction|Extraction completed for session" "$LATEST" | tail -n 40
```

**Request/response logging toggle (local):**
- Config section: `RequestResponseLogging` in `src/SessionSight.Api/appsettings.Development.json`
- Defaults: `Enabled=true`, `LogBodies=false`, `MaxBodyLogBytes=null`

**Deploy Bicep:**
```bash
USER_ID=$(az ad signed-in-user show --query id -o tsv)
az deployment sub create --location eastus2 --template-file infra/main.bicep \
  --parameters environmentName=dev developerUserObjectId=$USER_ID
```

**Development patterns:**
- `AgentLoopRunner` has two `RunAsync` overloads: DI-injected tools and explicit tool list
- `IAgentTool` implementations should use case-insensitive JSON deserialization
- Return `ToolResult.Error()` for invalid inputs instead of throwing
- `ExtractedField<T>` carries `Value`, `Confidence`, and `Source`
- Aspire env vars must be set in AppHost with `.WithEnvironment(...)`
- FluentValidation validators are auto-discovered via `AddValidatorsFromAssemblyContaining<>()`

## Lessons Learned

**Code analysis:**
- `CA1848`: use `[LoggerMessage]` delegates instead of direct `_logger.LogWarning()`
- `S6966`: use async file operations instead of sync versions

**SonarCloud and CI parity:**
- Local parity uses `SonarAnalyzer.CSharp` with warnings as errors
- Frontend parity is partial
- Shell and JSX false positives are handled by CI exclusions, not `NOSONAR`

**E2E tests:**
- Full-stack E2E catches frontend/backend type drift
- Extraction is `202 Accepted` plus polling
- Very fast extraction test results often indicate content filter blocking
- Retry on infrastructure signals, not LLM quality signals

**LLM pipeline:**
- Look at actual LLM output first before speculative fixes
- Use `.WithEnvironment()` in AppHost for Aspire env vars
- Set `ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()` for JSON responses
- Use narrow test filters while iterating

**Coverage:**
- Local threshold is `83%` for backend and frontend
- CI threshold is `80%`
- Backend script clears stale `coverage/`
- E2E tests do not contribute to code coverage

**Cloud deployment:**
- Azure SQL serverless can auto-pause; use a 60s+ timeout
- Container Apps can scale to zero and cold-start
- Managed Identity SQL auth uses client ID, not principal ID
- Go-based `sqlcmd` has T-SQL parsing limits
- `sqlcmd -b -V 11` catches syntax errors reliably
- `infra.yml` handles CI service principal Graph permission needs

**Local dev:**
- `start-dev.sh` and `start-aspire.sh` must export the venv PATH so `az` is found

**Git workflow:**
- Check for post-merge commits accidentally pushed to old feature branches

**Dependabot:**
- Batch merge may require `gh auth refresh -h github.com -s workflow`
