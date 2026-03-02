# Multi-Model Research Spike Report (B-107)

**Date:** March 2, 2026
**Result:** PASS WITH REVISED APPROACH
**Branch:** `feature/multi-model-agent-debate`

---

## Executive Summary

The original plan to use `Azure.AI.Inference` as a unified SDK for all models is **dead** — the package is deprecated (retiring May 30, 2026). Gemini is not available on Azure. Claude requires Enterprise/MCA-E subscription (user does not have this). However, the architecture is **dramatically simpler** than expected: Foundry marketplace models like Mistral, Llama, and Grok all use the **standard OpenAI-compatible `/chat/completions` API**. This means everything goes through `Microsoft.Extensions.AI.OpenAI` (GA) — no custom adapters needed, no additional SDKs. The debate just needs different model deployments, all callable through the same `IChatClient`.

---

## Pass/Fail Criteria Results

### 1. Claude chat completion — PASS (with different SDK)

Claude IS available in Azure AI Foundry (East US 2). Available models:
- Claude Opus 4.6, Opus 4.5, Opus 4.1
- Claude Sonnet 4.6, Sonnet 4.5
- Claude Haiku 4.5

**Critical finding:** Claude on Foundry uses the **Anthropic Messages API** (`/anthropic/v1/messages`), NOT the OpenAI-compatible `/chat/completions` endpoint. The `Azure.AI.Inference.ChatCompletionsClient` CANNOT call Claude.

**Required SDK:** `Anthropic.Foundry` NuGet package (not `Azure.AI.Inference`).

**Auth:** `DefaultAzureCredential` works via `AnthropicFoundryIdentityTokenCredentials`. Requires `Cognitive Services User` role on the Foundry resource.

**Code pattern:**
```csharp
using Anthropic.Foundry;
using Anthropic.Models.Messages;
using Azure.Identity;

var client = new AnthropicFoundryClient(
    new AnthropicFoundryIdentityTokenCredentials(
        new DefaultAzureCredential(),
        "sessionsight-openai-dev"  // Foundry resource name
    )
);

var response = await client.Messages.Create(new MessageCreateParams
{
    Model = "claude-sonnet-4-5",
    MaxTokens = 1024,
    Messages = [new() { Role = Role.User, Content = "Hello!" }],
});
```

### 2. Gemini availability — FAIL (not available)

**Gemini is NOT in Azure AI Foundry.** Google Gemini remains exclusive to Google Cloud's Vertex AI.

Available non-OpenAI models in Foundry (East US 2):
- **Anthropic:** Claude Opus/Sonnet/Haiku (4.1–4.6)
- **Meta:** Llama 3.x, Llama 4 Scout
- **Mistral:** Codestral, Ministral, Mistral Small/Medium
- **Cohere:** Command R, Embed
- **xAI:** Grok 3, Grok 3 Mini
- **DeepSeek:** DeepSeek models

**Revised judge options:** GPT-4.1-mini (independent from nano advocate), Mistral, or Grok.

### 3. IChatClient proof — PARTIAL PASS

**GPT path (GA, production-ready):**
```csharp
// Microsoft.Extensions.AI.OpenAI v10.3.0 (GA)
IChatClient gptClient = new AzureOpenAIClient(endpoint, credential)
    .GetChatClient("gpt-4.1-mini")
    .AsIChatClient();
```

**Claude path (requires custom adapter):**
The `Anthropic.Foundry` client does NOT implement `IChatClient`. No `Microsoft.Extensions.AI.Anthropic` adapter package exists. We must write our own `AnthropicChatClientAdapter : IChatClient` that:
- Translates `ChatMessage` → Anthropic `MessageCreateParams`
- Translates Anthropic `Message` response → `ChatResponse`
- Maps tool calling types between the two APIs
- Maps `UsageDetails` from Anthropic's `Usage` object

This is ~150-200 lines of adapter code. Not trivial but well-scoped.

**Azure.AI.Inference path (deprecated, DO NOT USE):**
`Azure.AI.Inference` is officially deprecated, retiring May 30, 2026. The `Microsoft.Extensions.AI.AzureAIInference` adapter (preview, stale since Nov 2025) depends on it. Both are dead ends. Remove from `Directory.Packages.props`.

### 4. AgentLoopRunner feasibility — PASS

Current `AgentLoopRunner.RunCoreAsync` uses these OpenAI-specific types:

| Current (OpenAI) | IChatClient equivalent | Notes |
|-------------------|----------------------|-------|
| `ChatClient` | `IChatClient` | Direct replacement |
| `chatClient.CompleteChatAsync()` | `client.GetResponseAsync()` | Different method name |
| `ChatCompletion` | `ChatResponse` | Different type name |
| `ChatToolCall` | `FunctionCallContent` (in message contents) | Different structure |
| `ChatTokenUsage` | `UsageDetails` (`.InputTokenCount`, `.OutputTokenCount`) | Property name changes |
| `ChatFinishReason.Stop` | `ChatFinishReason.Stop` | Same name, different type (struct vs enum) |
| `ChatFinishReason.ToolCalls` | `ChatFinishReason.ToolCalls` | Same |
| `ChatFinishReason.ContentFilter` | `ChatFinishReason.ContentFilter` | Same |
| `ChatResponseFormat.CreateJsonObjectFormat()` | `ChatResponseFormat.Json` or `ChatResponseFormat.ForJsonSchema()` | Slightly different API |
| `ChatCompletionOptions` | `ChatOptions` | Different type |
| `ChatTool.CreateFunctionTool()` | `AIFunctionFactory.Create()` | Different tool definition pattern |

The refactor is mechanical — type-for-type replacement. No architectural changes needed in the loop logic itself. The `FunctionInvokingChatClient` middleware could replace our manual tool loop, but we should keep our custom loop for control (content filter retry, per-round callbacks, trace capture).

**Key risk:** `RawRepresentation` escape hatch exists on `ChatResponse` — can access the underlying provider-specific response if the abstraction has gaps. For the OpenAI adapter, `response.AsOpenAIChatCompletion()` is available.

### 5. Bicep discovery — PASS

Claude deploys as `Microsoft.CognitiveServices/accounts/deployments` — **same resource type as OpenAI models**. The differentiator is `format: 'Anthropic'` instead of `'OpenAI'`.

```bicep
resource claudeDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: aiServices  // existing CognitiveServices account
  name: 'claude-sonnet-4-5'
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
  properties: {
    model: {
      format: 'Anthropic'
      name: 'claude-sonnet-4-5'
      version: '1'
    }
  }
}
```

**Prerequisites:**
- Enterprise or MCA-E subscription (hard requirement for Claude)
- Marketplace terms acceptance (one-time): `az term accept --publisher anthropic --product claude --plan claude-sonnet-4-5`
- Region: East US 2 or Sweden Central only
- `Cognitive Services User` role for Entra ID auth

**No new Bicep module needed** — add Claude deployments to existing `openai.bicep` (or rename to `ai-models.bicep`).

### 6. Cost estimate — PASS

Per-debate cost (2 rounds + judge, ~50K input / 5K output per call):

| Role | Model | Input | Output | Total |
|------|-------|-------|--------|-------|
| Advocate (2 rounds) | GPT-4.1-nano | $0.010 | $0.004 | $0.014 |
| Challenger (2 rounds) | Claude Haiku 4.5 | $0.100 | $0.050 | $0.150 |
| Judge (1 call) | GPT-4.1-mini | $0.020 | $0.008 | $0.028 |
| **Total per debate** | | | | **~$0.19** |

Using Claude Sonnet instead of Haiku as challenger: ~$0.52 per debate.

**Revised for Mistral as challenger:** Mistral Small pricing is comparable to GPT-4.1-mini (~$1/$3 per MTok). Per-debate cost drops to ~$0.05-0.08. Significantly cheaper than Claude path.

With `borderline` trigger (~20-30% of notes): adds ~$0.01-0.02 per average extraction.

---

## Revised Architecture (SIMPLIFIED)

**Key insight:** User does not have Enterprise/MCA-E subscription, so Claude is blocked. But the goal is just "different model family" — not specifically Claude. Foundry marketplace models (Mistral, Llama, Grok, DeepSeek) all use the standard OpenAI-compatible `/chat/completions` API. This means **one SDK handles everything**.

### SDK Layer
```
Microsoft.Extensions.AI v10.3.0 (GA)     ← IChatClient interface
  └─ Microsoft.Extensions.AI.OpenAI      ← SINGLE adapter for ALL models (GA v10.3.0)
        wraps: Azure.AI.OpenAI v2.1.0
        works with: GPT, Mistral, Llama, Grok, DeepSeek (all OpenAI-compatible)
```

No custom adapters. No Anthropic SDK. No dual-SDK architecture.

### Model Assignments (final)
| Role | Model | Family | Rationale |
|------|-------|--------|-----------|
| Advocate | GPT-4.1-nano | OpenAI | Defends initial assessment, cheap |
| Challenger | Mistral Small (or Llama 4 Scout) | Mistral/Meta | Different model family, different reasoning style, OpenAI-compatible API |
| Judge | GPT-4.1-mini | OpenAI | Independent from advocate (different tier), proven reliable |

All three go through the same `IChatClient` adapter — just different deployment names/endpoints.

### Packages to ADD
| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.AI` | 10.3.0 | Core IChatClient interface + middleware |
| `Microsoft.Extensions.AI.OpenAI` | 10.3.0 | Adapter for all OpenAI-compatible models |

### Packages to REMOVE
| Package | Reason |
|---------|--------|
| `Azure.AI.Inference` | Deprecated, retiring May 30, 2026 |
| `Azure.AI.Projects` | Only used by spike, not in production |
| `Azure.AI.Agents.Persistent` | Only used by spike, not in production |

### Packages UNCHANGED
| Package | Status |
|---------|--------|
| `Azure.AI.OpenAI` v2.1.0 | Still needed — underlying client for the M.E.AI adapter |

---

## Risks and Mitigations (simplified)

| Risk | Mitigation |
|------|-----------|
| Mistral/Llama model availability may vary by region | Verify deployment availability in East US 2 before Bicep. Multiple fallback options exist (Mistral, Llama, Grok, DeepSeek) |
| Marketplace terms acceptance for non-OpenAI models | One-time `az term accept` per model provider. Automate in deployment script |
| Different models may have different tool-calling quality | Test with golden files. If challenger model handles tools poorly, simplify debate to text-only (no tools needed for argumentation) |
| `strict` JSON schema not supported in M.E.AI abstraction | Debate produces narrative text + structured verdict. Parse verdict from response, don't rely on strict schema |
| Content filter behavior may differ across providers | All Foundry models go through Azure content filter. Handle `ChatFinishReason.ContentFilter` uniformly |

---

## Next Steps

1. **Deploy a non-OpenAI model** — `az cognitiveservices account deployment create` for Mistral Small or similar in the existing Foundry resource (East US 2). Accept marketplace terms first.
2. **Add packages** — `Microsoft.Extensions.AI` v10.3.0, `Microsoft.Extensions.AI.OpenAI` v10.3.0
3. **Remove deprecated packages** — `Azure.AI.Inference`, `Azure.AI.Projects`, `Azure.AI.Agents.Persistent` from `Directory.Packages.props`
4. **Refactor `AgentLoopRunner`** — `ChatClient` → `IChatClient` (mechanical type replacement, B-108)
5. **Extend `ModelRouter`** — return `ModelSelection` with deployment name + endpoint, add debate task types (B-109)
6. **Update Bicep** — add Mistral/challenger model deployment to `openai.bicep` (B-110)
7. **Build debate step** — configurable pipeline stage after RiskAssessor (B-111)

No custom adapters needed. No additional SDKs beyond M.E.AI.
