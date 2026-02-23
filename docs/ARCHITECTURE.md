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

---

## Sequence Diagrams

### 1. Extraction Pipeline (UI Upload)

The synchronous path: a user uploads a document through the browser, then triggers extraction. The orchestrator runs six sequential steps, two of which are non-fatal (summarization and search indexing).

```mermaid
sequenceDiagram
    participant Browser
    participant DocsCtrl as DocumentsController
    participant ExtCtrl as ExtractionController
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

    Note over Browser,DB: Phase 1 - Document Upload

    Browser->>DocsCtrl: POST document (multipart)
    DocsCtrl->>DocsCtrl: Validate size + extension
    DocsCtrl->>DocStore: UploadAsync(file)
    DocStore-->>DocsCtrl: blobUri
    DocsCtrl->>DB: AddDocumentAsync (status=Pending)
    DocsCtrl-->>Browser: 201 Created

    Note over Browser,DB: Phase 2 - Extraction Pipeline (5-min timeout)

    Browser->>ExtCtrl: POST extraction/sessionId
    ExtCtrl->>DB: GetByIdAsync(sessionId)
    ExtCtrl->>DB: TryTransition(Pending to Processing)
    alt Already processing or completed
        ExtCtrl-->>Browser: 409 Conflict
    end
    ExtCtrl->>Orch: ProcessSessionAsync(sessionId)

    Note over Orch,DB: Step 1 - Document Parse

    Orch->>DocStore: DownloadAsync(blobUri)
    DocStore-->>Orch: Stream
    Orch->>DocParser: ParseAsync(stream, fileName)
    DocParser-->>Orch: ParsedDocument

    Note over Orch,DB: Step 2 - Intake Validation (gate)

    Orch->>Intake: ProcessAsync(parsedDocument)
    Intake-->>Orch: IntakeResult
    alt Not a valid therapy note
        Orch->>DB: Status = Failed
        Orch-->>ExtCtrl: OrchestrationResult (Success=false)
        ExtCtrl-->>Browser: 200 failure result
    end

    Note over Orch,DB: Step 3 - Clinical Extraction (agent loop)

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

    Note over Orch,DB: Step 4 - Risk Assessment (re-extract + merge)

    Orch->>Risk: AssessAsync(extraction, noteText)
    Risk->>Risk: Re-extract risk fields (focused prompt)
    Risk->>Risk: Keyword safety net scan
    Risk->>Risk: Find discrepancies + conservative merge
    Note right of Risk: More-severe value wins<br/>Guardrails may downgrade<br/>if no evidence found
    Risk-->>Orch: RiskAssessmentResult
    Orch->>Orch: Overwrite extraction.RiskAssessment

    Note over Orch,DB: Step 5 - Summarization (non-fatal)

    Orch->>Summarizer: SummarizeSessionAsync(extraction)
    alt Success
        Summarizer-->>Orch: SessionSummary
    else Failure
        Summarizer-->>Orch: null (logged, continues)
    end

    Note over Orch,DB: Step 6 - Search Indexing (non-fatal)

    Orch->>Indexer: IndexSessionAsync(session, extraction, summary)
    Indexer->>Embedding: GenerateEmbeddingAsync(text)
    Embedding-->>Indexer: float[3072]
    Indexer->>Search: IndexDocumentAsync(searchDoc)
    alt Failure
        Note right of Indexer: Logged, continues
    end

    Note over Orch,DB: Final - Persist

    Orch->>DB: UpdateExtractionResultAsync(entity)
    Orch->>DB: Status = Completed
    Orch-->>ExtCtrl: OrchestrationResult (Success=true)
    ExtCtrl-->>Browser: 200 OrchestrationResult
```

The ExtractionController creates its own 5-minute `CancellationTokenSource`, decoupled from the HTTP request cancellation. This means the pipeline runs to completion even if the browser disconnects. On any unhandled exception, the orchestrator transitions the document status to Failed.

### 2. Extraction Pipeline (Blob Trigger)

The asynchronous ingestion path: an Azure Function watches the `incoming/` container. When a blob lands, it moves through `processing/` and `processed/` (or `failed/`) containers while the same orchestrator runs in the background.

```mermaid
sequenceDiagram
    participant Blob as AzureBlob [incoming]
    participant Func as ProcessIncomingNoteFunction
    participant IngCtrl as IngestionController
    participant PatientRepo as PatientRepository
    participant Orch as ExtractionOrchestrator
    participant DB

    Note over Blob,DB: Trigger - blob lands in incoming/patientId/fileName

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
    IngCtrl->>DB: CreateProcessingJob(jobKey)

    Note over IngCtrl,DB: Fire-and-forget (fresh DI scope)

    IngCtrl-->>Func: 202 Accepted (sessionId)
    IngCtrl-)Orch: Task.Run ProcessSessionAsync

    Note over Orch,DB: Background - same pipeline as Diagram 1 (Steps 1-6)

    alt Success
        Orch->>DB: Status = Completed
        Orch->>DB: Job status = Completed
        Func->>Blob: Move blob from processing/ to processed/
    else Failure
        Orch->>DB: Status = Failed
        Orch->>DB: Job status = Failed
        Func->>Blob: Move blob from processing/ to failed/
    end
```

The `GetOrCreateByExternalIdAsync` call uses a catch-and-retry pattern on unique constraint violation to handle concurrent blob triggers for the same patient. The `Task.Run` uses `IServiceScopeFactory` to create a fresh DI scope, ensuring the background work has its own DbContext and doesn't conflict with the HTTP request lifecycle.

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
