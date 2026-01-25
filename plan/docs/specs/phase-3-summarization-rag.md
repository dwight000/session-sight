# Phase 3: Summarization & RAG Spec

> **Goal**: Implement multi-level summaries and RAG-powered Q&A using Azure AI Search for vector embeddings.

## Prerequisites

- Phase 2 complete (extraction pipeline working)
- Azure AI Search configured
- Extracted sessions available in database

---

## Deliverables

1. Summarizer Agent (3 levels: session, patient, practice)
2. Azure AI Search vector index configuration
3. Embedding generation pipeline
4. Q&A Agent with RAG
5. Integration tests

---

## 1. Multi-Level Summaries

### Summary Levels & Triggering

| Level | Scope | Trigger | Use Case |
|-------|-------|---------|----------|
| **Session** | Single session | **Automatic** after extraction | Quick session review |
| **Patient** | All sessions for a patient | **On-demand** via API | Case conceptualization |
| **Practice** | All patients/sessions | **On-demand** via API | Supervisor dashboard |

**Triggering Details:**
- **Session summary**: Generated automatically at end of extraction pipeline. Stored with extraction.
- **Patient summary**: Generated when `GET /api/patients/{id}/summary` called. Can be cached with TTL.
- **Practice summary**: Generated when `GET /api/practice/summary?range=week` called. Expensive, consider caching.

### Summarizer Agent

```csharp
public class SummarizerAgent : ISessionSightAgent
{
    public async Task<SessionSummary> SummarizeSessionAsync(ExtractionResult extraction)
    {
        var modelId = _router.SelectModel(TaskType.Summarization);

        var prompt = SummarizationPrompts.GetSessionSummaryPrompt(extraction);

        var response = await _chatClient.CompleteAsync(
            new ChatMessage(ChatRole.User, prompt),
            new ChatCompletionOptions { ModelId = modelId },
            cancellationToken);

        return ParseSessionSummary(response);
    }

    public async Task<PatientSummary> SummarizePatientAsync(
        Guid patientId,
        DateRange? range = null)
    {
        var sessions = await _sessionRepository.GetSessionsWithExtractionsAsync(
            patientId, range);

        var prompt = SummarizationPrompts.GetPatientSummaryPrompt(sessions);

        // Use GPT-4o for multi-session synthesis
        var response = await _chatClient.CompleteAsync(...);

        return ParsePatientSummary(response);
    }

    public async Task<PracticeSummary> SummarizePracticeAsync(DateRange range)
    {
        var metrics = await _metricsRepository.GetPracticeMetricsAsync(range);
        var flaggedPatients = await _sessionRepository.GetFlaggedSessionsAsync(range);

        var prompt = SummarizationPrompts.GetPracticeSummaryPrompt(metrics, flaggedPatients);

        return ParsePracticeSummary(await _chatClient.CompleteAsync(...));
    }
}
```

### Summary DTOs

```csharp
public class SessionSummary
{
    public Guid SessionId { get; set; }
    public string OneLiner { get; set; } = string.Empty; // "Session 5: Mood 5/10, anxiety ongoing..."
    public string KeyPoints { get; set; } = string.Empty;
    public List<string> InterventionsUsed { get; set; } = new();
    public string NextSessionFocus { get; set; } = string.Empty;
    public RiskSummary? RiskFlags { get; set; }
}

public class PatientSummary
{
    public Guid PatientId { get; set; }
    public int TotalSessions { get; set; }
    public DateRange DateRange { get; set; }
    public string ProgressNarrative { get; set; } = string.Empty;
    public MoodTrend MoodTrend { get; set; }
    public List<string> EffectiveInterventions { get; set; } = new();
    public List<string> OngoingConcerns { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class PracticeSummary
{
    public DateRange DateRange { get; set; }
    public int TotalPatients { get; set; }
    public int TotalSessions { get; set; }
    public int PatientsNeedingReview { get; set; }
    public List<FlaggedPatientSummary> FlaggedPatients { get; set; } = new();
    public Dictionary<string, int> DiagnosisDistribution { get; set; } = new();
    public Dictionary<string, int> InterventionFrequency { get; set; } = new();
}
```

---

## 2. Azure AI Search Configuration

### What Gets Embedded?

**Decision: Embed summary + key extracted fields** (not raw note text)

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| Raw note text | Natural language matching | Long, noisy, inconsistent | ❌ |
| Extraction JSON | Structured, precise | Awkward for natural queries | ❌ |
| **Summary + key fields** | Best of both, concise, relevant | Requires good summaries | ✅ |

**Embedding composition:**
```
Session: {session_type} on {session_date}
Concerns: {presenting_concerns, secondary_concerns}
Interventions: {techniques_used}
Mood: {self_reported_mood}/10, Anxiety: {anxiety_level}
Diagnoses: {primary_diagnosis}
Progress: {progress_rating_overall}
Summary: {session_summary_key_points}
```

This creates a ~200-500 token embedding input that captures semantic meaning for Q&A.

### Patient Scoping

**All queries MUST be scoped to a specific patient** for privacy and relevance.

```csharp
// Q&A Agent - always filter by patient_id
var searchOptions = new SearchOptions
{
    Filter = $"patient_id eq '{patientId}'",  // REQUIRED
    VectorSearch = new VectorSearchOptions
    {
        Queries = { new VectorizedQuery(embedding) { KNearestNeighborsCount = 10 } }
    },
    SemanticSearch = new SemanticSearchOptions
    {
        SemanticConfigurationName = "semantic-config"
    }
};
```

Practice-level summaries use aggregations, NOT cross-patient search.

### Index Schema

```json
{
  "name": "sessionsight-sessions",
  "fields": [
    { "name": "id", "type": "Edm.String", "key": true },
    { "name": "session_id", "type": "Edm.String", "filterable": true },
    { "name": "patient_id", "type": "Edm.String", "filterable": true },
    { "name": "session_date", "type": "Edm.DateTimeOffset", "filterable": true, "sortable": true },
    { "name": "session_type", "type": "Edm.String", "filterable": true, "facetable": true },

    { "name": "content", "type": "Edm.String", "searchable": true },
    { "name": "summary", "type": "Edm.String", "searchable": true },

    { "name": "primary_diagnosis", "type": "Edm.String", "filterable": true, "facetable": true },
    { "name": "diagnoses", "type": "Collection(Edm.String)", "filterable": true },
    { "name": "interventions", "type": "Collection(Edm.String)", "filterable": true, "facetable": true },
    { "name": "concerns", "type": "Collection(Edm.String)", "searchable": true },

    { "name": "risk_level", "type": "Edm.String", "filterable": true, "facetable": true },
    { "name": "mood_score", "type": "Edm.Int32", "filterable": true },

    { "name": "content_vector", "type": "Collection(Edm.Single)", "dimensions": 3072, "vectorSearchConfiguration": "vector-config" }
  ],
  "vectorSearch": {
    "algorithms": [
      { "name": "hnsw-algorithm", "kind": "hnsw", "hnswParameters": { "m": 4, "efConstruction": 400, "efSearch": 500, "metric": "cosine" } }
    ],
    "profiles": [
      { "name": "vector-config", "algorithm": "hnsw-algorithm" }
    ]
  },
  "semantic": {
    "configurations": [
      {
        "name": "semantic-config",
        "prioritizedFields": {
          "contentFields": [{ "fieldName": "content" }, { "fieldName": "summary" }],
          "titleField": { "fieldName": "summary" }
        }
      }
    ]
  }
}
```

### Index Service

```csharp
public class SearchIndexService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;

    public async Task IndexSessionAsync(Session session, ExtractionResult extraction)
    {
        var embedding = await GenerateEmbeddingAsync(session, extraction);

        var document = new SearchDocument
        {
            ["id"] = session.Id.ToString(),
            ["session_id"] = session.Id.ToString(),
            ["patient_id"] = session.PatientId.ToString(),
            ["session_date"] = session.SessionDate.ToDateTime(TimeOnly.MinValue),
            ["session_type"] = session.SessionType.ToString(),
            ["content"] = session.Document?.ExtractedText,
            ["summary"] = extraction.Summary?.OneLiner,
            ["primary_diagnosis"] = extraction.Data.Diagnoses.PrimaryDiagnosis?.Value,
            ["interventions"] = extraction.Data.Interventions.TechniquesUsed?.Value?.Select(t => t.ToString()).ToList(),
            ["risk_level"] = extraction.Data.RiskAssessment.RiskLevelOverall?.Value?.ToString(),
            ["mood_score"] = extraction.Data.MoodAssessment.SelfReportedMood?.Value,
            ["content_vector"] = embedding
        };

        await _searchClient.IndexDocumentsAsync(
            IndexDocumentsBatch.Upload(new[] { document }));
    }

    private async Task<float[]> GenerateEmbeddingAsync(Session session, ExtractionResult extraction)
    {
        // Create embedding text from key fields
        var textForEmbedding = $"""
            Session: {session.SessionType} on {session.SessionDate}
            Concerns: {string.Join(", ", extraction.Data.PresentingConcerns.SecondaryConcerns?.Value ?? Array.Empty<string>())}
            Interventions: {string.Join(", ", extraction.Data.Interventions.TechniquesUsed?.Value?.Select(t => t.ToString()) ?? Array.Empty<string>())}
            Progress: {extraction.Data.TreatmentProgress.ProgressRatingOverall?.Value}
            Summary: {extraction.Summary?.KeyPoints}
            """;

        // Use Azure OpenAI text-embedding-3-large
        return await _embeddingClient.GenerateEmbeddingAsync(textForEmbedding);
    }
}
```

---

## 3. Q&A Agent with RAG

### Q&A Agent

```csharp
public class QAAgent : ISessionSightAgent
{
    private readonly IChatClient _chatClient;
    private readonly SearchClient _searchClient;
    private readonly IModelRouter _router;

    public async Task<QAResponse> AnswerAsync(
        string question,
        Guid? patientId = null,
        CancellationToken cancellationToken = default)
    {
        // Classify question complexity
        var complexity = await ClassifyQuestionAsync(question);
        var modelId = _router.SelectModel(
            complexity > 0.7 ? TaskType.ComplexQuery : TaskType.SimpleQuery);

        // Retrieve relevant context via RAG
        var context = await RetrieveContextAsync(question, patientId);

        // Generate answer
        var prompt = QAPrompts.GetAnswerPrompt(question, context);

        var response = await _chatClient.CompleteAsync(
            new ChatMessage(ChatRole.User, prompt),
            new ChatCompletionOptions { ModelId = modelId },
            cancellationToken);

        return new QAResponse
        {
            Question = question,
            Answer = response.Content,
            Sources = context.Select(c => c.ToSourceCitation()).ToList(),
            Confidence = CalculateAnswerConfidence(response, context)
        };
    }

    private async Task<List<SearchResult>> RetrieveContextAsync(
        string question,
        Guid? patientId)
    {
        // Generate embedding for question
        var questionEmbedding = await _embeddingClient.GenerateEmbeddingAsync(question);

        // Hybrid search: vector + keyword + semantic
        var searchOptions = new SearchOptions
        {
            QueryType = SearchQueryType.Semantic,
            SemanticConfigurationName = "semantic-config",
            VectorQueries =
            {
                new VectorizedQuery(questionEmbedding)
                {
                    KNearestNeighborsCount = 5,
                    Fields = { "content_vector" }
                }
            },
            Size = 10
        };

        if (patientId.HasValue)
        {
            searchOptions.Filter = $"patient_id eq '{patientId}'";
        }

        var results = await _searchClient.SearchAsync<SearchDocument>(
            question, searchOptions);

        return await results.GetResultsAsync().ToListAsync();
    }
}
```

### Context Limit (Overflow Handling)

**Problem:** Patients with 50+ sessions could return too much context, exceeding token limits.

**Solution:** Hard limit of 10 sessions with warning.

```csharp
private async Task<List<SearchResult>> RetrieveContextAsync(string question, Guid? patientId)
{
    const int MaxSessions = 10;

    var results = await _searchClient.SearchAsync<SearchDocument>(question, searchOptions);
    var sessions = await results.GetResultsAsync().Take(MaxSessions + 1).ToListAsync();

    // Check if we hit the limit
    if (sessions.Count > MaxSessions)
    {
        _logger.LogInformation(
            "Patient {PatientId} has more than {Max} relevant sessions. Older history excluded.",
            patientId, MaxSessions);

        return new ContextResult
        {
            Sessions = sessions.Take(MaxSessions).ToList(),
            Warning = $"Showing {MaxSessions} most relevant sessions. Patient has additional history not included.",
            TotalAvailable = await GetTotalSessionCountAsync(patientId)
        };
    }

    return new ContextResult { Sessions = sessions };
}
```

**Response includes warning when truncated:**
```json
{
  "answer": "Based on recent sessions...",
  "warning": "Showing 10 most relevant sessions. Patient has 47 total sessions not all included.",
  "sources": [...]
}
```

### Example Questions

| Question | Context Scope | Model |
|----------|---------------|-------|
| "What's the patient's current diagnosis?" | Single patient | GPT-4o-mini |
| "Which interventions have worked best?" | Single patient history | GPT-4o |
| "Show me all patients with sleep issues" | Practice-wide | GPT-4o |
| "How has mood changed over time?" | Patient longitudinal | GPT-4o |
| "Identify patterns across anxious patients" | Multi-patient analysis | GPT-4o |

---

## 4. API Endpoints

```csharp
[ApiController]
[Route("api/[controller]")]
public class SummaryController : ControllerBase
{
    [HttpGet("session/{sessionId:guid}")]
    public async Task<ActionResult<SessionSummary>> GetSessionSummary(Guid sessionId) { }

    [HttpGet("patient/{patientId:guid}")]
    public async Task<ActionResult<PatientSummary>> GetPatientSummary(
        Guid patientId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate) { }

    [HttpGet("practice")]
    public async Task<ActionResult<PracticeSummary>> GetPracticeSummary(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate) { }
}

[ApiController]
[Route("api/[controller]")]
public class QAController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<QAResponse>> Ask(
        [FromBody] QARequest request) { }

    [HttpPost("patient/{patientId:guid}")]
    public async Task<ActionResult<QAResponse>> AskAboutPatient(
        Guid patientId,
        [FromBody] QARequest request) { }
}
```

---

## 5. Verification Checklist

- [ ] Session summaries generate correctly
- [ ] Patient summaries synthesize multiple sessions
- [ ] Practice summaries aggregate metrics
- [ ] Azure AI Search index created and populated
- [ ] Vector embeddings generated correctly
- [ ] Hybrid search returns relevant results
- [ ] Q&A agent answers questions accurately
- [ ] Source citations are correct
- [ ] Patient filtering works in search
- [ ] API endpoints return expected data

---

---

## Exit Criteria (Phase Gates)

Phase 3 is complete when all criteria are met:

| Metric | Target | Measurement |
|--------|--------|-------------|
| RAG retrieval precision@5 | >0.80 | Eval harness with labeled queries |
| Q&A answer relevance | >0.75 | Human eval on 20 test questions |
| Embedding dimension | 3072 | Verify AI Search index schema |
| Summary generation | Working | All 3 levels (session, patient, practice) |
| Patient filter | Working | Q&A scoped to single patient returns only their data |

**Verification commands:**
```bash
# Check index dimensions
az search index show --name sessionsight-sessions --service-name $SEARCH_SERVICE | jq '.fields[] | select(.name=="content_vector") | .dimensions'

# Run RAG eval
dotnet test --filter "Category=RAGEval"
```

---

## Next Phase

After Phase 3 is complete, proceed to **Phase 4: Risk Dashboard & UI** (see `phase-4-risk-dashboard.md`).
