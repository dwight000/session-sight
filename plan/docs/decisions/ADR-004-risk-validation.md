# ADR-004: Risk Assessment Validation Strategy

**Status**: Accepted
**Date**: January 22, 2026

## Context

Risk assessment extraction (suicidal ideation, self-harm, homicidal ideation) is **safety-critical**. A false negative (missing risk indicators) could have serious consequences. We need a validation strategy that:
1. Tests extraction accuracy on known ground truth
2. Catches potential misses before they reach production
3. Flags uncertain extractions for human review

## Decision: Golden Files + Confidence Floor

### 1. Golden File Testing (Ground Truth)

Create synthetic therapy notes with **human-labeled risk fields** (minimum 30, actual implementation: 37):

```json
{
  "note_id": "risk-test-001",
  "note_content": "Patient expressed feeling hopeless and mentioned 'not wanting to be here anymore'...",
  "expected_extraction": {
    "suicidal_ideation": "passive",
    "si_frequency": "occasional",
    "self_harm": "none",
    "homicidal_ideation": "none",
    "risk_level_overall": "moderate"
  },
  "test_type": "must_detect_passive_si"
}
```

**Test requirements:**
- 100% match required on safety fields (SI, SH, HI)
- Zero false negatives allowed in test suite
- Test suite runs on every PR (CI gate)

### 2. Confidence Floor (Runtime Safety Net)

Any risk field with confidence < 0.9 triggers automatic human review flag:

```csharp
if (extraction.RiskAssessment.Confidence < 0.9)
{
    extraction.RequiresReview = true;
    extraction.ReviewReason = "Risk assessment confidence below threshold";
}
```

**Thresholds** (canonical source: `docs/specs/clinical-schema.md`):

| Field Category | Minimum Confidence | Action if Below |
|----------------|-------------------|-----------------|
| Risk Assessment | 0.9 | Flag for review |
| Session Info | 0.7 | Accept with warning |
| Other fields | 0.6 | Accept |

### 3. Test Coverage Matrix

| Scenario | Count | Purpose |
|----------|-------|---------|
| Active suicidal ideation | 5 | Must detect |
| Passive suicidal ideation | 5 | Must detect subtle |
| Self-harm indicators | 5 | Must detect |
| Homicidal ideation | 3 | Must detect |
| No risk indicators | 5 | Must NOT false positive |
| Ambiguous language | 5 | Should flag for review |
| Negated statements | 2 | "Denies SI" = none |
| **Minimum Total** | **30** | |

> **Note:** Implementation created 37 test cases, exceeding the minimum requirement.

### 4. CI Integration

```yaml
# .github/workflows/ci.yml
- name: Run Risk Assessment Tests
  run: dotnet test --filter "Category=RiskAssessment"
  # This job MUST pass - no exceptions
```

## Alternative Strategies (Fallback Options)

### Alternative A: Keyword Safety Net

If golden file testing proves insufficient, add secondary regex check:

```csharp
var dangerKeywords = new[] { "suicide", "kill myself", "end it all", "not worth living" };
if (dangerKeywords.Any(k => noteContent.Contains(k, StringComparison.OrdinalIgnoreCase)))
{
    if (extraction.SuicidalIdeation == "none")
    {
        extraction.RequiresReview = true;
        extraction.ReviewReason = "Keyword detected but AI extracted 'none'";
    }
}
```

### Alternative B: Dual Extraction

Run risk section through GPT-4o twice, compare results:

```csharp
var extraction1 = await ExtractRisk(note, prompt: "standard");
var extraction2 = await ExtractRisk(note, prompt: "safety-focused");

if (extraction1.SuicidalIdeation != extraction2.SuicidalIdeation)
{
    extraction.RequiresReview = true;
}
```

## Implementation Checklist

- [ ] Create golden file JSON schema
- [ ] Generate 30 synthetic test notes with labels
- [ ] Implement confidence threshold check in extractor
- [ ] Add `RequiresReview` flag to extraction model
- [ ] Create CI test job for risk assessment
- [ ] Document human review workflow

## References

- [Clinical Schema - Risk Assessment Fields](../specs/clinical-schema.md)
- [Phase 2 Spec](../specs/phase-2-ai-extraction.md)
