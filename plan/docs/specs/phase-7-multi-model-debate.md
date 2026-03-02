# Phase 7: Multi-Model Agent Debate

> Risk assessment validation via adversarial debate across model families (GPT + Claude)
>
> **Spike completed (B-107, March 2, 2026).** Key findings: Gemini not available on Azure. `Azure.AI.Inference` deprecated (retiring May 2026). Claude uses Anthropic Messages API, needs `Anthropic.Foundry` NuGet + custom `IChatClient` adapter. See `plan/spike/multi-model/SPIKE-REPORT.md` for full details.

## Motivation

### Portfolio Value
- **Agent debate pattern** — adversarial collaboration is a recognized multi-agent pattern rarely seen in portfolio projects
- **Multi-model architecture** — demonstrates model-agnostic design, not locked to one vendor
- **Microsoft.Extensions.AI** — the new standard abstraction for LLM clients in .NET
- **Cost-aware engineering** — configurable triggers avoid wasting compute on clear-cut cases

### Technical Value
- Risk assessment is safety-critical — false negatives (missing suicidal ideation) are dangerous
- Having agents argue FOR and AGAINST a risk flag forces both sides to cite specific evidence
- Different model families reason differently (GPT is structured/literal, Claude is nuanced/cautious) — the tension produces better outcomes
- The debate transcript becomes an audit trail explaining WHY a risk was flagged

## Architecture

### Current Pipeline
```
Intake → ClinicalExtractor → RiskAssessor(GPT-4.1-mini) → Summarizer → Embedding
```

### Proposed Pipeline (Option B — additive, configurable)
```
Intake → ClinicalExtractor → RiskAssessor(GPT-4.1-mini)
                                ↓
                          [Confidence borderline?] ──no──→ Summarizer → Embedding
                                ↓ yes
                          RiskDebate:
                            Round 1: Advocate (GPT-4.1-nano) presents risk evidence
                            Round 1: Challenger (Claude Sonnet via Foundry) presents counter-evidence
                            Round 2: Advocate rebuts
                            Round 2: Challenger rebuts
                            Judge (Gemini or GPT-4.1-mini): synthesizes → final risk level + confidence
                                ↓
                          Summarizer → Embedding
```

### Why Option B (not replacing RiskAssessor)
1. **Cost control** — debate only fires when needed (~20-30% of notes, tunable)
2. **Zero breaking change** — existing pipeline untouched, debate is purely additive
3. **Configurable** — feature flag + threshold slider (almost never fire ↔ almost always fire)
4. **Graceful degradation** — if Claude/Gemini is unavailable, falls back to single-pass assessment

### Model Assignments (final, post-spike)
| Role | Model | Family | Rationale |
|------|-------|--------|-----------|
| Advocate | GPT-4.1-nano | OpenAI | Same family as RiskAssessor — defends its initial assessment, cheap |
| Challenger | Mistral Small (or Llama 4 Scout) | Mistral / Meta | Different model family, different reasoning style, OpenAI-compatible API |
| Judge | GPT-4.1-mini | OpenAI | Independent from advocate (different tier), proven reliable |

All three use the same `IChatClient` adapter (`Microsoft.Extensions.AI.OpenAI`). No custom adapters needed.

**Why not Claude?** Requires Enterprise/MCA-E Azure subscription (user does not have this).
**Why not Gemini?** Not available on Azure AI Foundry — exclusive to Google Vertex AI.
**Why Mistral?** OpenAI-compatible API, available in Foundry, different model family. Configurable — can swap to Llama, Grok, or DeepSeek.

## SDK Layer Changes

### Current State
- Production uses `Azure.AI.OpenAI` (`OpenAI.Chat.ChatClient`) exclusively
- `AgentLoopRunner.RunCoreAsync` takes `ChatClient` — hardwired to OpenAI
- `ModelRouter.SelectModel()` returns a string deployment name — no provider info
- `Azure.AI.Inference` (`1.0.0-beta.5`) is in `Directory.Packages.props` but unused

### Target State (final, post-spike — SIMPLIFIED)
- `AgentLoopRunner` accepts `Microsoft.Extensions.AI.IChatClient` (model-agnostic)
- ALL models (GPT + Mistral/Llama/etc.) go through `Microsoft.Extensions.AI.OpenAI` adapter (GA v10.3.0)
- No custom adapters needed — all Foundry marketplace models use OpenAI-compatible API
- `ModelRouter` returns `ModelSelection` (deployment name + provider + endpoint)
- `Azure.AI.Inference` REMOVED (deprecated, retiring May 2026)

### Why This Is Simple (post-spike revelation)

The original concern was needing multiple SDKs for different model families. The spike found that **all non-Claude Foundry models use the OpenAI-compatible `/chat/completions` API**. Since Claude requires Enterprise subscription (unavailable), the remaining options (Mistral, Llama, Grok, DeepSeek) all work through the same SDK. One adapter handles everything.

### Key SDK Relationships (final)
```
Microsoft.Extensions.AI v10.3.0 (GA)     ← IChatClient interface
  └─ Microsoft.Extensions.AI.OpenAI      ← SINGLE adapter for ALL models (GA v10.3.0)
        wraps: Azure.AI.OpenAI v2.1.0
        works with: GPT-4.1-*, Mistral, Llama, Grok, DeepSeek

AgentLoopRunner                           ← currently takes ChatClient, will take IChatClient
ModelRouter                               ← currently returns string, will return ModelSelection
```

### Packages to ADD
| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.AI` | 10.3.0 | Core IChatClient + middleware |
| `Microsoft.Extensions.AI.OpenAI` | 10.3.0 | Adapter for all OpenAI-compatible models |

### Packages to REMOVE
| Package | Reason |
|---------|--------|
| `Azure.AI.Inference` | Deprecated, retiring May 30, 2026 |
| `Azure.AI.Projects` | Spike-only, never used in production |
| `Azure.AI.Agents.Persistent` | Spike-only, never used in production |

### Key Design Decision: Debate is Text-Only (No Tool Calling)

The debate agents receive the RiskAssessor's output as text input and argue purely through narrative. No tool calling needed. This means:
- **Any model works** regardless of tool-calling support (Llama is viable)
- Simpler prompts, lower cost, fewer failure modes
- The AgentLoopRunner's tool-calling refactor (B-108) is for the existing pipeline only — debate agents don't use tools

### Verified Model Availability (East US 2, March 2026)

Models sold **directly by Azure** (no marketplace subscription needed):
| Model | Provider | Format String | Tool Calling | Notes |
|-------|----------|---------------|-------------|-------|
| `Mistral-Large-3` | Mistral | `'Mistral AI'` | Yes | Best non-OpenAI option |
| `grok-3-mini` | xAI | `'xAI'` | Yes | Cheap, fast |
| `grok-3` | xAI | `'xAI'` | Yes | Stronger reasoning |
| `Llama-3.3-70B-Instruct` | Meta | `'Meta'` | No | Viable for text-only debate |
| `Llama-4-Maverick-17B-128E-Instruct-FP8` | Meta | `'Meta'` | No | Viable for text-only debate |

**All callable through the existing `Azure.AI.OpenAI` SDK** — same endpoint, same `DefaultAzureCredential`. Verified by Microsoft docs.

**Note:** These models are NOT yet deployed in the user's Foundry resource. B-110 adds the Bicep deployment.

### Known Risks (final)
- Non-OpenAI model not yet deployed in tenant — B-110 adds Bicep, may need manual first deploy to verify availability
- Content filter: all Foundry models go through Azure content filter — handle `ChatFinishReason.ContentFilter` uniformly
- Model reasoning quality varies — test debate output with golden files to ensure meaningful argumentation
- Token usage and content filter properties differ between SDKs — `AgentLoopRunner` needs per-provider handling

## Bicep / IaC Impact

### Current Bicep
- `infra/modules/openai.bicep` — deploys `Microsoft.CognitiveServices/accounts` with 4 model deployments (gpt-4.1, gpt-4.1-mini, gpt-4.1-nano, text-embedding-3-large)
- `infra/modules/aiHub.bicep` — AI Foundry Hub
- `infra/modules/aiProject.bicep` — AI Foundry Project

### New Bicep Needed (confirmed by spike)
- **All Foundry models use same resource type** — `Microsoft.CognitiveServices/accounts/deployments`
- No new Bicep module needed — add challenger deployment to existing `openai.bicep` (consider renaming to `ai-models.bicep`)
- Verified Bicep format strings: `'Mistral AI'`, `'xAI'`, `'Meta'`, `'OpenAI'`
- Bicep example:
```bicep
resource challengerDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openai
  name: 'Mistral-Large-3'
  sku: { name: 'GlobalStandard', capacity: 1 }
  properties: {
    model: { format: 'Mistral AI', name: 'Mistral-Large-3', version: '1' }
  }
}
```

## Configuration

```json
{
  "RiskDebate": {
    "Enabled": true,
    "TriggerMode": "borderline",
    "ConfidenceThreshold": [0.3, 0.7],
    "MaxRounds": 2,
    "AdvocateModel": "gpt-4.1-nano",
    "ChallengerModel": "Mistral-Large-3",
    "JudgeModel": "gpt-4.1-mini"
  }
}
```

| TriggerMode | Behavior |
|-------------|----------|
| `always` | Every risk assessment goes through debate |
| `borderline` | Only when RiskAssessor confidence is within threshold range |
| `flagged` | Only when RiskAssessor flags a risk (any confidence) |
| `off` | Debate disabled — single-pass only |

## Cost Estimate

Estimated per-debate cost (~5K input / 2K output tokens per call, 5 calls total):

| Component | Model | Est. per-call | Calls | Total |
|-----------|-------|--------------|-------|-------|
| Advocate | GPT-4.1-nano ($0.10/$0.40 MTok) | ~$0.001 | 2 | $0.002 |
| Challenger | Mistral-Large-3 (~$0.50/$1.50 MTok) | ~$0.006 | 2 | $0.012 |
| Judge | GPT-4.1-mini ($0.40/$1.60 MTok) | ~$0.005 | 1 | $0.005 |
| **Total per debate** | | | | **~$0.02** |

With `borderline` trigger (~20-30% of notes): adds ~$0.004-0.006 per average extraction.

**Cheaper alternatives:** Llama-3.3-70B (~$0.15/$0.60 MTok) or grok-3-mini (~$0.30/$0.50 MTok) as challenger would reduce per-debate cost to ~$0.01.

## Prior Art in This Repo

| Resource | Location | Relevance |
|----------|----------|-----------|
| Agent Framework research | `plan/docs/research/azure-ai-foundry-agent-research-jan2026.md` | Claude availability in Foundry, NuGet packages, API patterns |
| Aspire AI research | `plan/docs/research/aspire-ai-capabilities-research-2026.md` | Aspire + Foundry integration, multi-model support |
| Agent Framework spike | `plan/spike/agent-framework/` | Tested `Azure.AI.Agents.Persistent` — PASS. Different SDK from production but validated Foundry connectivity |
| ModelRouter | `src/SessionSight.Agents/Routing/ModelRouter.cs` | Current 3-tier GPT routing — will be extended |
| AgentLoopRunner | `src/SessionSight.Agents/Tools/AgentLoopRunner.cs` | Core agent loop — the main refactor target (B-108) |
| Directory.Packages.props | root | `Azure.AI.Inference` already pinned at `1.0.0-beta.5` |
| Bicep AI modules | `infra/modules/openai.bicep`, `aiHub.bicep`, `aiProject.bicep` | Existing IaC for OpenAI + Foundry Hub/Project |

## Implementation Order

```
B-107 (Spike) ──→ B-108 (AgentLoopRunner refactor) ──→ B-109 (ModelRouter) ──→ B-111 (Debate step)
       └──→ B-110 (Bicep) ─────────────────────────────────────────────────────→ B-111
B-111 ──→ B-112 (Config) + B-113 (UI) + B-114 (Tests)
```

B-107 is the gate — everything depends on the spike findings. If Claude/Gemini aren't available or `Microsoft.Extensions.AI` has deal-breaking gaps, we pivot.

## Lessons from Phase 2 (Agent Framework Instability)

During the original agent framework spike (B-001), we encountered:
- `MessageRole.Assistant` renamed to `MessageRole.Agent` between SDK betas
- Ambiguous overloads appearing between `beta.7` and `beta.8`
- Package name churn (`Microsoft.Agents.*` vs `Azure.AI.Agents`)
- Decided to build custom `AgentLoopRunner` instead of using unstable Foundry persistent agents

**Lesson applied:** The B-107 spike must validate the exact SDK versions and API surface BEFORE committing to the refactor. Pin versions immediately. Document any quirks. If `Azure.AI.Inference` or `Microsoft.Extensions.AI` adapters are unstable, consider building a thin custom `IChatModelClient` instead.

## Multi-Agent Pattern Reference

The agent debate pattern goes by several names in the literature:
- **Agent debate / Multi-agent debate (MAD)** — formal research term
- **Adversarial collaboration** — emphasizes the cooperative goal
- **Critic pattern** — simplified version (propose → critique → revise)
- **Red team / blue team** — security framing
- **Deliberative alignment** — framework terminology

### Why Structured Debate with Judge (Decision)

Six patterns were evaluated for risk assessment specifically:

| Pattern | How it works | Verdict |
|---------|-------------|---------|
| **Structured debate + judge** | Advocate vs. challenger, fixed rounds, judge decides | **Chosen** — maps naturally to binary risk decision (flag vs. don't flag), fixed rounds prevent circles, judge gives clear final answer, transcript is an audit trail |
| Round-robin discussion | 3+ agents speak in fixed order, N rounds | Rejected — risk is binary, a third perspective doesn't add much. Agents start echoing each other |
| Free-form group chat | Dynamic moderator picks who speaks next | Rejected — overkill for binary decision, highest risk of going in circles, hard to control termination |
| Mixture of Agents / voting | All assess independently, aggregator synthesizes | Considered — simpler but no adversarial tension. Agents may all agree and miss edge cases |
| Critic pattern | One proposes, one critiques, proposer revises | Close second — but asymmetric (critic helps, doesn't argue opposing position). Debate is stronger for safety-critical |
| Red team / blue team | One attacks, one defends | Essentially what we're doing — debate is the generalized form |

The structured debate produces the strongest portfolio story: visible adversarial reasoning, explicit evidence-citing, audit trail, and three model families cooperating on a safety-critical decision.
