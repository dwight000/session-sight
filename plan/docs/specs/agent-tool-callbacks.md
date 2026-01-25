# Agent-to-Tool Callbacks Specification

> **Purpose**: Define how agents call tools, reason about results, and iteratively refine their outputs. This is a key portfolio demo feature showing production AI patterns.

## Overview

Unlike simple prompt-response AI, SessionSight agents:
1. Receive a task
2. Call tools to gather information
3. Reason about tool results
4. Decide next steps
5. Iterate until complete

This demonstrates **agentic AI** - the agent has autonomy to decide which tools to call and when.

---

## Tool Definitions

### Extractor Agent Tools

| Tool | Purpose | Input | Output |
|------|---------|-------|--------|
| `validate_schema` | Check extraction against schema | Extraction JSON | Validation errors/warnings |
| `query_patient_history` | Get prior sessions for context | Patient ID, count | Array of session summaries |
| `score_confidence` | Calculate confidence scores | Extraction + source text | Per-field confidence |
| `lookup_diagnosis_code` | Validate ICD-10/DSM codes | Diagnosis text | ICD-10 code or null |
| `check_risk_keywords` | Scan for safety keywords | Text | Risk keyword matches |

### Q&A Agent Tools

| Tool | Purpose | Input | Output |
|------|---------|-------|--------|
| `search_sessions` | Vector search across sessions | Query, patient filter | Relevant session chunks |
| `get_session_detail` | Full session extraction | Session ID | Complete extraction |
| `get_patient_timeline` | Chronological patient view | Patient ID | Timeline of key events |
| `aggregate_metrics` | Calculate statistics | Patient ID, metric type | Aggregated values |

### Summarizer Agent Tools

| Tool | Purpose | Input | Output |
|------|---------|-------|--------|
| `get_mood_trend` | Calculate mood trajectory | Patient ID, date range | Trend data + direction |
| `identify_effective_interventions` | Find what's working | Patient ID | Ranked interventions |
| `compare_sessions` | Diff two sessions | Session IDs | Changes between |

---

## Implementation Pattern

### Tool Interface

```csharp
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    JsonSchema InputSchema { get; }
    Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct);
}

public record ToolResult(
    bool Success,
    JsonElement Data,
    string? ErrorMessage = null
);
```

### Tool Registration

```csharp
public class ExtractorAgent
{
    private readonly List<IAgentTool> _tools = new()
    {
        new ValidateSchemaool(),
        new QueryPatientHistoryTool(),
        new ScoreConfidenceTool(),
        new LookupDiagnosisCodeTool(),
        new CheckRiskKeywordsTool()
    };

    public string GetToolsPrompt()
    {
        return string.Join("\n", _tools.Select(t =>
            $"- {t.Name}: {t.Description}\n  Input: {t.InputSchema}"));
    }
}
```

### Agent Loop

```csharp
public async Task<ExtractionResult> ExtractAsync(IntakeResult intake)
{
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, GetSystemPrompt()),
        new(ChatRole.User, $"Extract clinical data from:\n{intake.CleanedText}")
    };

    while (true)
    {
        var response = await _chatClient.CompleteAsync(messages, new ChatCompletionOptions
        {
            Tools = _tools.Select(t => t.ToToolDefinition()).ToList(),
            ToolChoice = ToolChoice.Auto
        });

        messages.Add(response.Message);

        // Check if agent wants to call tools
        if (response.ToolCalls?.Any() == true)
        {
            foreach (var toolCall in response.ToolCalls)
            {
                var tool = _tools.First(t => t.Name == toolCall.Name);
                var result = await tool.ExecuteAsync(toolCall.Arguments);

                messages.Add(new ChatMessage(ChatRole.Tool, result.Data.ToString())
                {
                    ToolCallId = toolCall.Id
                });
            }
            continue; // Let agent reason about tool results
        }

        // Agent is done - parse final response
        if (response.StopReason == StopReason.EndTurn)
        {
            return ParseExtractionResponse(response.Content);
        }
    }
}
```

---

## Example Execution Flow

### Scenario: Extracting a Progress Note

**Input Note:**
```
Patient reports mood as 5/10, down from last session. Mentioned feeling
overwhelmed at work. Denies SI/SH. Continued CBT work on thought records.
Dx: GAD (F41.1), rule out MDD.
```

**Agent Execution:**

```
[Turn 1] Agent reasons:
"I need to extract clinical data. Let me first check for risk keywords
since safety is critical."

[Tool Call] check_risk_keywords(text: "...Denies SI/SH...")
[Tool Result] { "si_mentioned": true, "denial": true, "keywords": ["SI", "SH", "Denies"] }

[Turn 2] Agent reasons:
"SI/SH mentioned but denied. Confidence is high on denial context.
Let me get patient history to see if risk has been present before."

[Tool Call] query_patient_history(patient_id: "P001", count: 3)
[Tool Result] {
  "sessions": [
    { "date": "2026-01-08", "risk_level": "low", "si": "none" },
    { "date": "2026-01-01", "risk_level": "low", "si": "none" }
  ]
}

[Turn 3] Agent reasons:
"Patient has no history of SI. Current denial is consistent. I'll extract
with high confidence on risk being low. Now let me validate the diagnosis code."

[Tool Call] lookup_diagnosis_code(text: "GAD")
[Tool Result] { "code": "F41.1", "description": "Generalized anxiety disorder" }

[Turn 4] Agent reasons:
"Code F41.1 confirmed in note. MDD is marked as rule-out. I have enough
information to complete the extraction."

[Final Response]
{
  "mood_assessment": {
    "self_reported_mood": { "value": 5, "confidence": 0.95 },
    "mood_change_from_last": { "value": "declined", "confidence": 0.90 }
  },
  "risk_assessment": {
    "suicidal_ideation": { "value": "none", "confidence": 0.95 },
    "self_harm": { "value": "none", "confidence": 0.95 }
  },
  "diagnoses": {
    "primary_diagnosis": { "value": "Generalized anxiety disorder", "confidence": 0.98 },
    "primary_diagnosis_code": { "value": "F41.1", "confidence": 1.0 },
    "rule_outs": { "value": ["MDD"], "confidence": 0.85 }
  },
  "interventions": {
    "techniques_used": { "value": ["cbt"], "confidence": 0.95 }
  }
}
```

---

## Key Implementation Details

### 1. Tool Choice Strategy

```csharp
// Let agent decide which tools to call
ToolChoice = ToolChoice.Auto

// Force a specific tool (for testing)
ToolChoice = ToolChoice.Required("validate_schema")

// No tools (final answer only)
ToolChoice = ToolChoice.None
```

### 2. Iteration Limits

**CRITICAL: When hitting the limit, return a partial result with review flag - never return null/empty.**

```csharp
const int MaxToolCalls = 15;  // Increased from 10 for complex extractions
int toolCallCount = 0;
ExtractionResult partialResult = new();  // Accumulate results

while (true)
{
    if (++toolCallCount > MaxToolCalls)
    {
        _logger.LogWarning("Agent hit tool call limit after {Count} calls", toolCallCount);

        // Return partial result with review flag
        partialResult.RequiresReview = true;
        partialResult.ReviewReason = $"Extraction incomplete - tool limit ({MaxToolCalls}) exceeded";
        partialResult.CompletionStatus = CompletionStatus.Partial;
        partialResult.CompletedSections = GetCompletedSections(partialResult);

        return partialResult;  // DON'T just break - return something valid
    }

    // ... process tools, accumulate into partialResult
}
```

**Why 15 instead of 10?** Clinical extraction across 82 fields may need:
- `validate_schema` (1)
- `extract_session_info` (1)
- `extract_presenting_concerns` (1)
- `extract_mood_assessment` (1)
- `extract_risk_assessment` (1)
- `extract_mental_status` (1)
- `extract_interventions` (1)
- `extract_diagnoses` (1)
- `extract_treatment_progress` (1)
- `extract_next_steps` (1)
- `query_patient_history` (1)
- `refine_extraction` (1-2)
- Buffer for retries (2-3)

**Monitoring:** Add metric for "% extractions hitting tool limit" to track if limit is too low.

### 3. Parallel Tool Calls

When multiple tools are called, execute them in parallel:

```csharp
if (response.ToolCalls?.Count > 1)
{
    var tasks = response.ToolCalls.Select(async tc =>
    {
        var tool = _tools.First(t => t.Name == tc.Name);
        var result = await tool.ExecuteAsync(tc.Arguments);
        return (tc.Id, result);
    });

    var results = await Task.WhenAll(tasks);
    foreach (var (id, result) in results)
    {
        messages.Add(new ChatMessage(ChatRole.Tool, result.Data.ToString())
        {
            ToolCallId = id
        });
    }
}
```

### 4. Error Handling

```csharp
try
{
    var result = await tool.ExecuteAsync(toolCall.Arguments);
    messages.Add(new ChatMessage(ChatRole.Tool, result.Data.ToString()));
}
catch (Exception ex)
{
    // Tell agent the tool failed - it can decide how to proceed
    messages.Add(new ChatMessage(ChatRole.Tool,
        JsonSerializer.Serialize(new { error = ex.Message })));
}
```

---

## Demo Value

This pattern demonstrates:

1. **Agentic Reasoning** - Agent decides what tools to call based on context
2. **Multi-Step Processing** - Not just one prompt-response, but iterative refinement
3. **Tool Use** - Agents can query databases, validate data, call APIs
4. **Production Patterns** - This is how enterprise AI systems work
5. **Safety-First** - Risk checking happens before other extraction

**Portfolio Impact**: Shows you understand how to build real AI systems, not just call an API.

---

## Telemetry

Log each agent turn for debugging and demo:

```csharp
_logger.LogInformation(
    "Agent Turn {Turn}: {ToolCalls} tool calls, reasoning: {Reasoning}",
    turnNumber,
    response.ToolCalls?.Count ?? 0,
    response.Content?.Substring(0, 100));

// OpenTelemetry span for observability
using var span = _tracer.StartSpan($"agent.{Name}.turn.{turnNumber}");
span.SetAttribute("tool_calls", toolCallCount);
```

The Aspire GenAI Telemetry Visualizer will show this execution flow in the dashboard.
