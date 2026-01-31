# B-001 Spike Report: Microsoft Agent Framework

**Date:** January 26, 2026
**Duration:** 1 session
**Result:** PASS

## 1. SDK Choice
- **Selected:** Option A (Foundry Agents via `Azure.AI.Projects` + `Azure.AI.Agents.Persistent`)
- **Rationale:** Foundry-based approach worked on first attempt. No need for Option B fallback. The `PersistentAgentsClient` provides a clean abstraction over the Assistants-style API with sub-clients for agents, threads, messages, runs, and files. Tool calling loop is straightforward.

## 2. Packages Tested

| Package | Version | Status |
|---------|---------|--------|
| Azure.AI.Projects | 1.2.0-beta.5 | Works (used for AIProjectClient) |
| Azure.AI.Agents.Persistent | 1.2.0-beta.8 | Works (primary agent SDK) |
| Azure.Identity | 1.17.1 | Works (DefaultAzureCredential via az CLI) |
| OpenAI | 2.8.0 | Transitive dep, works |
| System.ClientModel | 1.8.1 | Transitive dep, works |
| Aspire.Hosting.AppHost | 9.2.1 | Works (Aspire template) |
| Aspire.Hosting.Azure.CognitiveServices | 9.2.1 | Works (AddAzureOpenAI) |

## 3. Infrastructure Created

| Resource | Name | Purpose |
|----------|------|---------|
| AI Hub | sessionsight-ai-hub | Hub workspace (eastus2) |
| AI Project | sessionsight-ai-project | Foundry project under hub |
| Connection | sessionsight-openai-connection | Hub→OpenAI resource link |
| Key Vault secret | openai-api-key | Stored in sessionsight-kv-1792 |

**Agents Endpoint:**
```
https://eastus2.api.azureml.ms/agents/v1.0/subscriptions/71a2d8cf-f3b2-41d5-a813-001c2cbe6df1/resourceGroups/rg-sessionsight-dev/providers/Microsoft.MachineLearningServices/workspaces/sessionsight-ai-project
```

## 4. Working Code Sample

See `AgentSpike/Program.cs` — complete 280-line console app that:

1. Connects to AI Foundry project via `PersistentAgentsClient` + `DefaultAzureCredential`
2. Defines a `validate_schema` tool (`FunctionToolDefinition` with JSON Schema parameters)
3. Creates a clinical extraction agent with GPT-4o and a detailed system prompt
4. Creates a thread with a realistic therapy session note
5. Runs the agent, which autonomously calls `validate_schema`
6. Handles the tool call, returns mock validation result
7. Agent processes validation and returns structured JSON
8. Verifies all 4 pass criteria programmatically

**Key API patterns discovered:**
- `MessageRole.Agent` (not `.Assistant`) — SDK uses "Agent" terminology
- `SubmitToolOutputsToRunAsync` requires explicit `cancellationToken:` named parameter to disambiguate overloads
- Tool parameters must use `JsonNamingPolicy.CamelCase` for JSON Schema
- `RunStatus.RequiresAction` + `SubmitToolOutputsAction` pattern for tool calls
- `AsyncPageable<PersistentThreadMessage>` for reading message history
- Cleanup requires explicit `DeleteThreadAsync` + `DeleteAgentAsync`

## 5. Tool Calling Verification

- [x] Tool definition compiles and is accepted by the API
- [x] Agent calls `validate_schema` tool autonomously (not forced)
- [x] Tool result is fed back to agent correctly (run resumes after submission)
- [x] Final output is valid structured JSON matching clinical schema subset

**Actual output from the spike run:**
```json
{
  "clientId": "CLIENT-001",
  "sessionDate": "2026-01-15",
  "sessionType": "Individual",
  "presentingIssues": ["anxiety in social situations", "sleep difficulties", "feeling overwhelmed by performance review"],
  "interventionsUsed": ["Cognitive restructuring", "Exposure hierarchy", "Mindfulness breathing exercise"],
  "moodRating": 5,
  "riskLevel": "low",
  "riskIndicators": ["feeling overwhelmed by performance review"],
  "progressNotes": "Client showing good engagement with CBT techniques...",
  "nextSessionPlan": "Continue CBT focus on social anxiety..."
}
```

## 6. Aspire Integration

- [x] SpikeAppHost compiles with AgentSpike project referenced
- [x] `AddAzureOpenAI("openai").AddDeployment(...)` + `WithReference(openai)` wires correctly

**Notes:**
- Used `Aspire.Hosting.Azure.CognitiveServices` 9.2.1 (latest stable)
- `AddDeployment(AzureOpenAIDeployment)` is deprecated; use the 3-parameter overload instead
- Aspire workload version 9.2.1 installed (latest via `dotnet workload install aspire`)

## 7. Issues Encountered

| # | Issue | Resolution |
|---|-------|------------|
| 1 | `DefaultAzureCredential` couldn't find `az` CLI | `az` is installed in Python venv; added venv to `PATH` |
| 2 | `MessageRole.Assistant` doesn't exist | SDK uses `MessageRole.Agent` — this is a breaking rename vs older docs |
| 3 | `SubmitToolOutputsToRunAsync` ambiguous overload | New overload added in beta.8 with `IEnumerable<ToolApproval>` parameter; use `cancellationToken: default` named arg |
| 4 | Hub auto-created separate Key Vault + Storage Account | Expected — can be cleaned up or reused. Not a blocker. |
| 5 | OpenAI resource needed manual connection to hub | Created `az ml connection create` with `type: azure_open_ai` |

None of these issues are blockers. All were resolved in-session.

## 8. Recommendation

**GO** — proceed with Phase 1 and B-025 (pin versions).

### Rationale:
- Option A (Foundry Agents) works as documented
- Tool calling is reliable — agent autonomously invokes tools without forcing
- API is ergonomic with sub-client pattern (`.Administration`, `.Threads`, `.Messages`, `.Runs`)
- Aspire integration compiles cleanly
- All 4 pass criteria met on first real attempt (only fix was API surface differences)

### Pinned versions for B-025:
```xml
<PackageReference Include="Azure.AI.Projects" Version="1.2.0-beta.5" />
<PackageReference Include="Azure.AI.Agents.Persistent" Version="1.2.0-beta.8" />
<PackageReference Include="Azure.Identity" Version="1.17.1" />
```

### Risk notes for Phase 2:
- These are preview packages — API may change before GA
- `MessageRole.Agent` rename suggests Microsoft is actively evolving the API surface
- The `ToolApproval` overloads indicate human-in-the-loop features being added (useful for supervisor review)
- Monitor Azure SDK changelog for breaking changes
