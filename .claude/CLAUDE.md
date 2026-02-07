# SessionSight Project Guide

## Before Pushing Code

**REQUIRED before `git push`:**
```bash
# 1. Check coverage threshold (82% local, 81% CI, 80% SonarCloud) - MUST PASS
./scripts/check-coverage.sh

# 2. Run frontend tests (TypeScript + Vitest + build) - MUST PASS
./scripts/check-frontend.sh

# 3. Run E2E tests - MUST PASS
./scripts/run-e2e.sh
```

If coverage fails, add more unit tests before pushing.

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

# E2E tests (requires Azure services)
./scripts/run-e2e.sh

# E2E hot mode (keeps Aspire running between runs)
./scripts/run-e2e.sh --hot

# E2E with test filter
./scripts/run-e2e.sh --filter TestName
```

## E2E Troubleshooting

**Before running E2E:** Always run `dotnet test --filter "Category!=Functional"` first. Unit tests are fast and free — catch compilation/logic errors before spending money on LLM calls.

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

### Before Pushing
1. `dotnet build` - verify no Sonar/CA errors
2. `./scripts/check-coverage.sh` - must pass 82%
3. `./scripts/check-frontend.sh` - TypeScript + Vitest + build must pass
4. `./scripts/run-e2e.sh` - all functional tests must pass

### Memory vs Plan Files
- If you would write to auto memory but we are actively working with a BACKLOG.md or plan file, write the note there instead. Lessons learned go in CLAUDE.md, process reminders go in BACKLOG.md.
