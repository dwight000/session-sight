# SessionSight Project Guide

## Architecture Overview

**Pipeline:** Document → IntakeAgent → ClinicalExtractorAgent → RiskAssessorAgent → Database

**Agents:**
- `IntakeAgent` - Validates document is a therapy note
- `ClinicalExtractorAgent` - Extracts 82 fields using agent loop + tools
- `RiskAssessorAgent` - Safety validation of risk fields

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

**Logs:** `/tmp/aspire-e2e.log` (view live: `tail -f /tmp/aspire-e2e.log`)

**Common issues:**
- "No backend service" → Check Bicep deployment for AI Foundry connection
- "401/credential" errors → Run `az login`, verify Cognitive Services User role
- Port conflicts → `pkill -f SessionSight`

## Agent Tool Pattern

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
