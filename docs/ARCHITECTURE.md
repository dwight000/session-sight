# SessionSight Architecture

SessionSight is a clinical note analysis platform that uses an AI agent pipeline to extract structured data from therapy session notes. Documents enter via UI upload or Azure Blob trigger, pass through intake validation, clinical extraction (82 fields), risk assessment, summarization, and search indexing before results are persisted.

## System Overview

### Model Assignments

| Task | Model | Temperature |
|------|-------|-------------|
| Document Intake | gpt-4.1-nano | 0.1 |
| Clinical Extraction | gpt-4.1-mini | 0.1 |
| Risk Assessment | gpt-4.1-mini | 0.1 |
| Summarization | gpt-4.1-nano | 0.3 |
| Q&A Complexity Classifier | gpt-4.1-nano | 0.0 |
| Q&A Simple | gpt-4.1-nano | 0.2 |
| Q&A Complex | gpt-4.1-mini | 0.2 |
| Embeddings | text-embedding-3-large | - |

### Pipeline Summary

Document &rarr; IntakeAgent (gate) &rarr; ClinicalExtractorAgent (82 fields via tool loop) &rarr; RiskAssessorAgent (re-extract + conservative merge) &rarr; SummarizerAgent &rarr; SessionIndexingService &rarr; Database

Extraction is triggered asynchronously via `ExtractionJobDispatcher` (bounded channel, 3 concurrent workers). Steps 1&ndash;4 are fatal; steps 5&ndash;6 are non-fatal (failures yield `PartiallyCompleted` status with automatic resume on retry).

---

## Sequence Diagrams

### 1. Extraction Pipeline (UI Upload)

A user uploads a document through the browser, then triggers extraction. The controller enqueues the job on the `ExtractionJobDispatcher` and returns `202 Accepted` immediately. The browser polls for step-level progress while the orchestrator runs six sequential steps in the background, two of which are non-fatal (summarization and search indexing).

```mermaid
sequenceDiagram
    participant Browser
    participant DocsCtrl as DocumentsController
    participant ExtCtrl as ExtractionController
    participant Dispatcher as ExtractionJobDispatcher
    participant Orch as ExtractionOrchestrator
    participant DocStore as DocumentStorage
    participant DocParser as DocumentParser
    participant Intake as IntakeAgent [nano]
    participant Extractor as ClinicalExtractorAgent [mini]
    participant Runner as AgentLoopRunner
    participant Risk as RiskAssessorAgent [mini]
    participant Summarizer as SummarizerAgent [nano]
    participant Indexer as SessionIndexingService
    participant Embedding as EmbeddingService [e3-large]
    participant Search as AISearch
    participant DB

    Note over Browser,DB: Phase 1 — Document Upload

    Browser->>DocsCtrl: POST document (multipart)
    DocsCtrl->>DocsCtrl: Validate size + extension
    DocsCtrl->>DocStore: UploadAsync(file)
    DocStore-->>DocsCtrl: blobUri
    DocsCtrl->>DB: AddDocumentAsync (status=Pending)
    DocsCtrl-->>Browser: 201 Created

    Note over Browser,DB: Phase 2 — Trigger Extraction (async)

    Browser->>ExtCtrl: POST /api/extraction/{sessionId}
    ExtCtrl->>DB: GetByIdAsync(sessionId)
    ExtCtrl->>DB: TryTransitionDocumentStatusAsync
    Note right of ExtCtrl: Tries Pending→Processing,<br/>then Failed→Processing,<br/>then PartiallyCompleted→Processing<br/>(atomic SQL WHERE, RowVersion)
    alt Already processing or completed
        ExtCtrl-->>Browser: 409 Conflict
    end
    ExtCtrl->>Dispatcher: EnqueueAsync(sessionId)
    ExtCtrl-->>Browser: 202 Accepted {sessionId}

    Note over Browser,DB: Phase 2b — Browser Polling

    loop Every few seconds until terminal status
        Browser->>DocsCtrl: GET /api/sessions/{sessionId}/extraction/steps
        DocsCtrl->>DB: GetStepsByExtractionIdAsync
        DocsCtrl-->>Browser: {steps[], documentStatus, failureKind, errorMessage}
    end

    Note over Dispatcher,DB: Phase 3 — Background Processing

    Dispatcher->>Dispatcher: Channel reader dequeues job
    Dispatcher->>Dispatcher: CreateScope (fresh DI per job)
    Dispatcher->>Orch: ProcessSessionAsync(sessionId)

    Note over Orch,DB: Step 1 — Document Parse

    Orch->>DocStore: DownloadAsync(blobUri)
    DocStore-->>Orch: Stream
    Orch->>DocParser: ParseAsync(stream, fileName)
    DocParser-->>Orch: ParsedDocument

    Note over Orch,DB: Step 2 — Intake Validation (gate)

    Orch->>Intake: ProcessAsync(parsedDocument)
    Intake-->>Orch: IntakeResult
    alt Not a valid therapy note
        Orch->>DB: Status = Failed, FailureKind = Permanent
        Orch-->>Dispatcher: OrchestrationResult (Success=false)
    end

    Note over Orch,DB: Step 3 — Clinical Extraction (agent loop)

    Orch->>Extractor: ExtractAsync(intakeResult)
    Extractor->>Runner: RunAsync(messages, JSON format)
    loop Up to 15 tool calls, 5-min timeout
        Runner->>Runner: LLM call, check for tool_calls
        Runner->>Runner: Execute tools in parallel (Task.WhenAll)
        Note right of Runner: validate_and_score<br/>check_risk_keywords<br/>lookup_diagnosis_code<br/>query_patient_history
    end
    Runner-->>Extractor: AgentLoopResult
    Extractor->>Extractor: Parse to ClinicalExtraction (82 fields)
    Extractor-->>Orch: ExtractionResult

    Note over Orch,DB: Step 4 — Risk Assessment (re-extract + merge)

    Orch->>Risk: AssessAsync(extraction, noteText)
    Risk->>Risk: Re-extract risk fields (focused prompt)
    Risk->>Risk: Keyword safety net scan
    Risk->>Risk: Find discrepancies + conservative merge
    Note right of Risk: More-severe value wins<br/>Guardrails may downgrade<br/>if no evidence found
    Risk-->>Orch: RiskAssessmentResult
    Orch->>Orch: Overwrite extraction.RiskAssessment

    Note over Orch,DB: Step 5 — Summarization (non-fatal)

    Orch->>Summarizer: SummarizeSessionAsync(extraction)
    alt Success
        Summarizer-->>Orch: SessionSummary
    else Failure
        Summarizer-->>Orch: null (logged, continues)
    end

    Note over Orch,DB: Step 6 — Search Indexing (non-fatal)

    Orch->>Indexer: IndexSessionAsync(session, extraction, summary)
    Indexer->>Embedding: GenerateEmbeddingAsync(text)
    Embedding-->>Indexer: float[3072]
    Indexer->>Search: IndexDocumentAsync(searchDoc)
    alt Failure
        Note right of Indexer: Logged, continues
    end

    Note over Orch,DB: Final — Persist + Determine Status

    Orch->>DB: SaveExtractionAsync(entity)
    alt All 6 steps succeeded
        Orch->>DB: Status = Completed
    else Step 5 or 6 failed (non-fatal)
        Orch->>DB: Status = PartiallyCompleted
    end
    Orch-->>Dispatcher: OrchestrationResult

    alt Fatal failure in steps 1-4
        Orch->>Orch: ClassifyFailure(exception)
        Note right of Orch: Transient: 429, 5xx, auth,<br/>circuit breaker, JSON parse,<br/>content filter, timeout<br/>Permanent: invalid doc, blob 404
        Orch->>DB: Status = Failed + FailureKind + ErrorMessage
        Orch-->>Dispatcher: OrchestrationResult (Success=false)
    end
```

The `ExtractionController` atomically transitions the document status from `Pending`, `Failed`, or `PartiallyCompleted` to `Processing` via `TryTransitionDocumentStatusAsync` (SQL `WHERE Status = @expected` with `RowVersion` concurrency). If the transition succeeds, it enqueues the session ID on the `ExtractionJobDispatcher` and returns `202 Accepted`. The browser then polls `GET /api/sessions/{sessionId}/extraction/steps` for step-level progress, document status, failure kind, and error message.

The `ExtractionJobDispatcher` is a `BackgroundService` that reads from a bounded `Channel<ExtractionJob>` (capacity 20) with up to 3 concurrent workers. Each worker creates a fresh DI scope via `IServiceScopeFactory`, resolves the orchestrator, and calls `ProcessSessionAsync`. On fatal failure (steps 1&ndash;4), the orchestrator calls `ClassifyFailure` to categorize the exception as `Transient` (rate limit, 5xx, credential, circuit breaker, JSON parse, content filter, timeout) or `Permanent` (invalid document, blob not found) and persists the `FailureKind` and `ErrorMessage` on the document. On non-fatal failure (steps 5&ndash;6: summarization or search indexing), the document transitions to `PartiallyCompleted`. A subsequent extraction trigger for a `PartiallyCompleted` document resumes from the first failed non-fatal step via `CanResumeFromExistingExtraction`, skipping the already-succeeded core steps (1&ndash;4).

### 2. Extraction Pipeline (Blob Trigger)

The asynchronous ingestion path: an Azure Function watches the `incoming/` container. When a blob lands, it moves through `processing/` and `processed/` (or `failed/`) containers while the `ExtractionJobDispatcher` runs the same orchestrator pipeline in the background.

```mermaid
sequenceDiagram
    participant Blob as AzureBlob [incoming]
    participant Func as ProcessIncomingNoteFunction
    participant IngCtrl as IngestionController
    participant PatientRepo as PatientRepository
    participant Dispatcher as ExtractionJobDispatcher
    participant DB

    Note over Blob,DB: Trigger — blob lands in incoming/patientId/fileName

    Blob->>Func: BlobTrigger fires
    Func->>Func: Validate size (max 50 MB) + extension
    alt Invalid file
        Func->>Blob: Move blob to failed/
        Note right of Func: Return early
    end

    Func->>Blob: Move blob from incoming/ to processing/
    Func->>Func: ComputeJobKey (SHA256 of path + eTag)
    Func->>Func: ParseDateFromFileName

    Func->>IngCtrl: POST /api/ingestion/process

    Note over IngCtrl,DB: Idempotency + Setup

    IngCtrl->>DB: GetByJobKeyAsync(jobKey)
    alt Job already exists
        IngCtrl-->>Func: 202 Accepted (no-op)
    end

    IngCtrl->>PatientRepo: GetOrCreateByExternalIdAsync(patientId)
    PatientRepo-->>IngCtrl: Patient (atomic upsert)
    IngCtrl->>DB: Create Session + Document (status=Pending)
    IngCtrl->>DB: CreateProcessingJob(jobKey, status=Processing)
    Note right of IngCtrl: DbUpdateException on duplicate<br/>jobKey → 202 Accepted (idempotent)

    Note over IngCtrl,DB: Enqueue for background processing

    IngCtrl->>Dispatcher: EnqueueAsync(sessionId, jobKey)
    IngCtrl-->>Func: 202 Accepted (sessionId)

    Note over Dispatcher,DB: Background — same pipeline as Diagram 1 (Phase 3, Steps 1-6)

    alt Success
        Dispatcher->>DB: Status = Completed (or PartiallyCompleted)
        Dispatcher->>DB: Job status = Completed (or PartiallyCompleted)
        Func->>Blob: Move blob from processing/ to processed/
    else Fatal failure
        Dispatcher->>DB: Status = Failed + FailureKind + ErrorMessage
        Dispatcher->>DB: Job status = Failed
        Func->>Blob: Move blob from processing/ to failed/
    else Shutdown cancellation
        Dispatcher->>DB: Status = Failed, FailureKind = Transient
        Note right of Dispatcher: "Server shutting down —<br/>retry automatically"
    end
```

The `GetOrCreateByExternalIdAsync` call uses a catch-and-retry pattern on unique constraint violation to handle concurrent blob triggers for the same patient. Instead of `Task.Run` with manual scope creation, the `IngestionController` enqueues the job on the `ExtractionJobDispatcher` via `EnqueueAsync(sessionId, jobKey)`. The dispatcher's bounded channel (capacity 20, 3 concurrent workers) processes the extraction in a fresh DI scope. After the orchestrator completes, the dispatcher checks the `JobKey` and updates the `ProcessingJob` status to `Completed`, `PartiallyCompleted`, or `Failed`. On shutdown cancellation, the dispatcher marks the document as `Failed` with `FailureKind.Transient` and the message "Server shutting down &mdash; retry automatically" using a non-cancellable token (best effort).

### 3. Q&A Dual-Path Flow

The Q&A system classifies each question as simple or complex, then routes to the appropriate path. Simple questions use single-shot RAG with vector search. Complex questions use an agentic loop with five specialized tools.

```mermaid
sequenceDiagram
    participant Browser
    participant QACtrl as QAController
    participant QA as QAAgent
    participant LLM_Nano as gpt-4.1-nano
    participant LLM_Mini as gpt-4.1-mini
    participant Runner as AgentLoopRunner
    participant Embed as EmbeddingService [e3-large]
    participant Search as AISearch
    participant DB

    Browser->>QACtrl: POST /api/qa/patient/patientId
    QACtrl->>DB: GetByIdAsync(patientId)
    alt Patient not found
        QACtrl-->>Browser: 404 Not Found
    end
    QACtrl->>QA: AnswerAsync(question, patientId)

    Note over QA,LLM_Nano: Step 1 - Classify Complexity (temp=0.0)

    QA->>LLM_Nano: ComplexityPrompt + question
    LLM_Nano-->>QA: simple or complex

    alt Simple Path (nano, single-shot RAG)

        Note over QA,Search: Embed + Search

        QA->>Embed: GenerateEmbeddingAsync(question)
        Embed-->>QA: queryVector
        QA->>Search: SearchAsync(question, vector, patientId, limit=11)
        Search-->>QA: ranked results (max 10 used)

        alt No results found
            QA-->>QACtrl: No session data available
        end

        QA->>QA: BuildContextString(results)
        QA->>LLM_Nano: Context + question (temp=0.2)
        LLM_Nano-->>QA: Answer text
        QA-->>QACtrl: QAResponse (simple path)

    else Complex Path (mini, agentic loop)

        Note over QA,DB: Set patient isolation on tools

        QA->>QA: Set RequiredPatientId / AllowedPatientId on scoped tools

        QA->>Runner: RunAsync(messages, tools, temp=0.2)
        loop Up to 15 tool calls, 5-min timeout
            Runner->>LLM_Mini: Messages + tool definitions
            LLM_Mini-->>Runner: tool_calls or final answer
            Runner->>Runner: Execute tools in parallel
            Note right of Runner: search_sessions<br/>get_session_detail<br/>get_patient_timeline<br/>aggregate_metrics<br/>compare_sessions
        end
        Runner-->>QA: AgentLoopResult
        QA->>QA: Extract sources (cited sessions or tool trace fallback)
        QA-->>QACtrl: QAResponse (complex path)

    end

    QACtrl-->>Browser: 200 OK QAResponse
```

Patient isolation is enforced at the tool level: `search_sessions`, `get_session_detail`, and `compare_sessions` have a `RequiredPatientId`/`AllowedPatientId` property set before the loop starts, ensuring the LLM cannot access data from other patients regardless of the arguments it generates.

### 4. Agent Loop Runner

The `AgentLoopRunner` is the shared execution engine used by both the extraction agent and the complex Q&A path. It manages the LLM &harr; tool call loop with guards against runaway execution.

```mermaid
sequenceDiagram
    participant Agent
    participant Runner as AgentLoopRunner
    participant LLM
    participant ToolA as Tool A
    participant ToolB as Tool B

    Agent->>Runner: RunAsync(chatClient, messages, tools)

    Note over Runner: Create linked CTS (5-min timeout)

    loop Each round (while toolCallCount < 15)
        Runner->>LLM: CompleteChatAsync(messages, options)
        LLM-->>Runner: ChatCompletion

        Runner->>Runner: Accumulate token counts + record LlmCallTrace

        alt finish_reason = stop
            Runner-->>Agent: AgentLoopResult.Complete(content)
        else tool_calls in response
            Runner->>Runner: Add AssistantMessage to history

            par Execute tools in parallel
                Runner->>ToolA: ExecuteAsync(args)
                ToolA-->>Runner: ToolResult
            and
                Runner->>ToolB: ExecuteAsync(args)
                ToolB-->>Runner: ToolResult
            end

            Runner->>Runner: Add ToolMessages to history
            Runner->>Runner: toolCallCount += n
            Note right of Runner: Continue loop
        else Unexpected finish reason
            Runner-->>Agent: AgentLoopResult.Partial(reason)
        end
    end

    Note over Runner: toolCallCount >= 15

    Runner-->>Agent: AgentLoopResult.Partial (max tool calls reached)

    Note over Runner: If 5-min timeout fires

    Runner-->>Agent: AgentLoopResult.Partial (timed out after 5 minutes)
```

Two overloads serve different use cases:
- **DI-injected tools** (extraction): `RunAsync(chatClient, messages, responseFormat, temperature, ct)` uses tools registered in the DI container (validate_and_score, check_risk_keywords, lookup_diagnosis_code, query_patient_history).
- **Explicit tool list** (Q&A): `RunAsync(chatClient, messages, tools, temperature, ct)` accepts an `IEnumerable<IAgentTool>` for the five Q&A tools with patient-scoped access.

Unknown tool names return `ToolResult.Error()` rather than throwing, allowing the LLM to self-correct in subsequent rounds. Partial results (from timeout or max tool calls) are surfaced to the caller, which can decide whether to return them to the user or flag for review.

---

## Data Flow Diagrams

### 5. Document Lifecycle

The `SessionDocument.Status` state machine governs the entire extraction pipeline. All transitions use `TryTransitionDocumentStatusAsync` with an atomic `UPDATE ... WHERE Status = @expected` and `RowVersion` optimistic concurrency.

```mermaid
stateDiagram-v2
    [*] --> Pending : Document uploaded<br/>(UI or blob trigger)

    Pending --> Processing : TryTransitionDocumentStatusAsync<br/>(ExtractionController or Orchestrator)

    Processing --> Completed : All 6 steps succeeded
    Processing --> PartiallyCompleted : Steps 1–4 OK,<br/>step 5 or 6 failed
    Processing --> Failed : Fatal error in steps 1–4

    Failed --> Processing : Retry triggered<br/>(user or blob re-trigger)
    PartiallyCompleted --> Processing : Resume triggered<br/>(CanResumeFromExistingExtraction<br/>skips core steps 1–4)

    Completed --> [*]

    state Failed {
        [*] --> Transient : Rate limit, 5xx, auth, circuit breaker,<br/>JSON parse, content filter, timeout
        [*] --> Permanent : Invalid document, blob 404
    }
```

A `SessionDocument` progresses through five statuses. The `Failed` state carries a `FailureKind` (`Transient` or `Permanent`) and a user-facing `ErrorMessage`. Transient failures (rate limits, service outages, authentication errors, content filter, timeouts) are retryable. Permanent failures (invalid document, missing blob) require manual intervention. The `PartiallyCompleted` state indicates that core extraction succeeded (steps 1&ndash;4) but one or more non-fatal steps (summarization, search indexing) failed. Resuming from `PartiallyCompleted` calls `CanResumeFromExistingExtraction`, which checks that all four core steps have `Succeeded` status, then runs only the failed non-fatal steps.

### 6. Data Transformation Pipeline

How raw document bytes become structured clinical data, search vectors, and database entities across the six pipeline steps.

```mermaid
flowchart LR
    subgraph Input
        PDF["PDF / DOCX / Image"]
    end

    subgraph Step1["Step 1 · Document Parse"]
        DocIntel["Azure Document<br/>Intelligence"]
        Parsed["ParsedDocument<br/>MarkdownContent +<br/>PageCount + OcrConfidence"]
    end

    subgraph Step2["Step 2 · Intake"]
        IntakeAg["IntakeAgent<br/>gpt-4.1-nano"]
        IR["IntakeResult<br/>isValid · docType ·<br/>sessionDate · language"]
    end

    subgraph Step3["Step 3 · Clinical Extraction"]
        ExtAg["ClinicalExtractorAgent<br/>gpt-4.1-mini"]
        Loop["AgentLoopRunner<br/>≤15 tool calls"]
        ER["ClinicalExtraction<br/>82 fields as<br/>ExtractedField‹T›<br/>Value + Confidence + Source"]
    end

    subgraph Step4["Step 4 · Risk Assessment"]
        RiskAg["RiskAssessorAgent<br/>gpt-4.1-mini"]
        Merge["Conservative Merge<br/>+ Keyword Safety Net"]
        RA["RiskAssessmentResult<br/>riskLevel · discrepancies ·<br/>guardrail decisions"]
    end

    subgraph Step5["Step 5 · Summarization"]
        SumAg["SummarizerAgent<br/>gpt-4.1-nano"]
        SS["SessionSummary<br/>oneLiner ·<br/>interventionsUsed"]
    end

    subgraph Step6["Step 6 · Search Indexing"]
        EmbSvc["EmbeddingService<br/>text-embedding-3-large"]
        Vec["float 3072"]
        SearchDoc["SearchDocument<br/>text + vector + metadata"]
    end

    subgraph Persist["Database"]
        ExtRes["ExtractionResult<br/>+ ExtractionSteps<br/>+ SummaryJson"]
        DocStatus["SessionDocument<br/>Status + FailureKind"]
        SearchIdx["Azure AI Search<br/>Index"]
    end

    PDF --> DocIntel --> Parsed
    Parsed --> IntakeAg --> IR
    IR --> ExtAg --> Loop --> ER
    ER --> RiskAg --> Merge --> RA
    RA --> SumAg --> SS
    RA --> EmbSvc --> Vec --> SearchDoc

    ER --> ExtRes
    RA --> ExtRes
    SS --> ExtRes
    SearchDoc --> SearchIdx
    ExtRes --> DocStatus
```

Raw document bytes enter the pipeline and undergo six transformations. Steps 1&ndash;4 are fatal: a failure at any point halts the pipeline and classifies the failure. Steps 5&ndash;6 are non-fatal: failures result in `PartiallyCompleted` status. Each step records its progress as an `ExtractionStep` entity with status, timing, model used, token counts, and optional LLM traces. The `ClinicalExtraction` schema contains 82 fields, each wrapped in `ExtractedField<T>` with `Value`, `Confidence` (0&ndash;1), and `Source` (quoted text from the note). The risk assessment stage re-extracts risk-specific fields with a focused prompt, runs a keyword safety net scan, then conservatively merges results (more-severe value wins). The final `ExtractionResult` entity aggregates extraction data, summary JSON, risk audit columns, and per-field risk decisions.

### 7. Entity Relationship

Core domain entities and their relationships. Navigation properties shown as relationship lines; key columns listed inside each entity.

```mermaid
erDiagram
    Patient ||--o{ Session : has
    Session ||--o| SessionDocument : has
    Session ||--o| ExtractionResult : has
    Session }o--|| Therapist : "assigned to"
    ExtractionResult ||--o{ ExtractionStep : contains
    ExtractionResult ||--o{ SupervisorReview : "reviewed by"
    ExtractionStep ||--o{ ExtractionToolCall : records
    ExtractionStep ||--o{ ExtractionLlmTrace : records

    Patient {
        Guid Id PK
        string ExternalId UK
        string FirstName
        string LastName
        DateOnly DateOfBirth
    }

    Session {
        Guid Id PK
        Guid PatientId FK
        Guid TherapistId FK
        DateOnly SessionDate
        SessionType SessionType
        SessionModality Modality
        byte_array RowVersion
    }

    SessionDocument {
        Guid Id PK
        Guid SessionId FK
        DocumentStatus Status
        FailureKind FailureKind
        IndexingStatus IndexingStatus
        string ErrorMessage
        string BlobUri
        string OriginalFileName
        byte_array RowVersion
    }

    ExtractionResult {
        Guid Id PK
        Guid SessionId FK
        double OverallConfidence
        bool RequiresReview
        ReviewStatus ReviewStatus
        string SummaryJson
        bool GuardrailApplied
        int DiscrepancyCount
    }

    ExtractionStep {
        Guid Id PK
        Guid ExtractionId FK
        ExtractionStepName StepName
        ExtractionStepStatus Status
        int StepOrder
        long DurationMs
        string ModelUsed
        int TotalTokens
    }

    ExtractionToolCall {
        Guid Id PK
        Guid StepId FK
        string ToolName
        int LoopRound
        bool Succeeded
        long DurationMs
    }

    ExtractionLlmTrace {
        Guid Id PK
        Guid StepId FK
        string ModelUsed
        int LoopRound
        int TotalTokens
        long DurationMs
    }

    SupervisorReview {
        Guid Id PK
        Guid ExtractionId FK
        ReviewStatus Action
        string ReviewerName
        string Notes
    }

    Therapist {
        Guid Id PK
        string Name
    }

    ProcessingJob {
        Guid Id PK
        string JobKey UK
        JobStatus Status
    }
```

A `Patient` has many `Sessions`. Each `Session` has at most one `SessionDocument` (the uploaded file) and at most one `ExtractionResult` (the structured extraction output). An `ExtractionResult` contains up to six `ExtractionStep` records, one per pipeline stage. Each step may have `ExtractionToolCall` records (for the agent loop's tool invocations) and `ExtractionLlmTrace` records (raw LLM request/response pairs, gated by `PipelineDiagnosticsOptions.StoreLlmTraces`). `SupervisorReview` captures human review decisions on flagged extractions. `ProcessingJob` tracks blob-triggered ingestion jobs by `JobKey` (SHA256 of blob path + eTag) for idempotency &mdash; it is not linked to `Session` by foreign key but is correlated by the `ExtractionJobDispatcher` at runtime. Both `Session` and `SessionDocument` use `RowVersion` for optimistic concurrency.
