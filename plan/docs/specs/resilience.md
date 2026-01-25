# Resilience Patterns Specification

> **Purpose**: Define resilience patterns for handling transient failures, rate limits, and ensuring data consistency across the ingestion pipeline.

## Overview

SessionSight must handle:
- Azure OpenAI rate limits (429 errors)
- Transient network failures (5xx errors)
- Duplicate blob events (at-least-once delivery)
- Data sync between Blob → SQL → AI Search

---

## 1. Exponential Backoff for Azure OpenAI

### Pattern
Use exponential backoff with jitter for 429 (rate limit) and 5xx (server error) responses.

### Configuration
```
Base delay: 1 second
Max delay: 60 seconds
Max retries: 5
Jitter: ±20%
Retryable: 429, 500, 502, 503, 504
```

### Implementation Reference
- Azure OpenAI SDK has built-in retry via `Azure.Core.RetryOptions`
- Configure in DI: `AddAzureOpenAI().ConfigureHttpClient()`
- See: [Azure SDK retry guidance](https://learn.microsoft.com/en-us/azure/architecture/best-practices/retry-service-specific)

### When to Apply
- All Azure OpenAI calls (extraction, summarization, embeddings)
- All Azure AI Search calls

---

## 2. Circuit Breaker

### Pattern
After repeated failures, stop calling the service temporarily to allow recovery.

### Configuration
```
Failure threshold: 5 failures in 30 seconds
Break duration: 60 seconds
Half-open test: 1 request
```

### Implementation Reference
- Use Polly library: `AddResilienceHandler()` in .NET 8+
- Or Microsoft.Extensions.Http.Resilience

### When to Apply
- Azure OpenAI endpoint
- Azure AI Search endpoint
- External services only (not internal APIs)

---

## 3. Idempotent Job Processing

### Problem
Blob triggers can fire multiple times for the same blob (at-least-once delivery).

### Pattern
Use idempotent job IDs based on blob path + ETag.

### Implementation
```
Job ID = SHA256(container + blobPath + ETag)
```

**CRITICAL: Use atomic check-and-insert to prevent race conditions.**

The naive check-then-insert pattern has a TOCTOU (time-of-check-time-of-use) race:
```
Thread A: SELECT → not found
Thread B: SELECT → not found
Thread A: INSERT ✓
Thread B: INSERT ✓ ← DUPLICATE!
```

### Correct Pattern: SQL MERGE with HOLDLOCK

```sql
MERGE ProcessingJobs WITH (HOLDLOCK) AS target
USING (SELECT @JobId AS JobId) AS source
ON target.JobId = source.JobId
WHEN NOT MATCHED THEN
    INSERT (JobId, BlobPath, Status, StartedAt)
    VALUES (@JobId, @BlobPath, 'Processing', GETUTCDATE())
WHEN MATCHED AND target.Status = 'Completed' THEN
    -- Already done, no update needed
    UPDATE SET JobId = target.JobId  -- No-op to satisfy MERGE syntax
WHEN MATCHED AND target.Status = 'Processing'
    AND DATEDIFF(MINUTE, target.StartedAt, GETUTCDATE()) > 10 THEN
    -- Stuck job, allow retry
    UPDATE SET StartedAt = GETUTCDATE(), Status = 'Processing'
OUTPUT $action, inserted.Status;
```

**Decision logic based on OUTPUT:**
- `$action = 'INSERT'` → Process the document
- `$action = 'UPDATE'` with `Status = 'Processing'` → Retry stuck job
- `$action = 'UPDATE'` with `Status = 'Completed'` → Skip (already done)

### Database Schema
```sql
CREATE TABLE ProcessingJobs (
    JobId NVARCHAR(64) PRIMARY KEY,
    BlobPath NVARCHAR(500),
    Status NVARCHAR(20),  -- Processing, Completed, Failed
    StartedAt DATETIME2,
    CompletedAt DATETIME2,
    ErrorMessage NVARCHAR(MAX),
    INDEX IX_ProcessingJobs_Status (Status, StartedAt)  -- For stuck job queries
);
```

### Timeout Handling
- Jobs stuck in Processing for >10 minutes are auto-retried via MERGE
- Add `LastHeartbeat` column for very long-running jobs (>1 hour)

---

## 4. Dead-Letter / Poison Queue Handling

### Pattern
After max retries, move failed items to a dead-letter location for manual review.

### Implementation
1. Blob trigger fails 3 times → move blob to `failed/` container
2. Log failure details to Application Insights
3. Create alert for items in `failed/` container

### Failed Container Structure
```
failed/
└── {patient-id}/
    └── {original-filename}/
        ├── original.txt       # The original file
        ├── error.json         # Error details
        └── metadata.json      # Processing context
```

### Error Tracking
```json
{
  "jobId": "abc123",
  "blobPath": "incoming/P001/session-2026-01-21.txt",
  "attempts": 3,
  "lastError": "Azure OpenAI rate limit exceeded",
  "failedAt": "2026-01-21T15:30:00Z"
}
```

---

## 5. Reindex / Replay Jobs

### Problem
AI Search index can drift from SQL source of truth due to:
- Failed indexing after successful extraction
- Schema changes requiring re-embedding
- Embedding model updates

### Pattern
Scheduled reconciliation job compares SQL → AI Search.

### Implementation
```
1. Query SQL: SELECT Id, UpdatedAt FROM Sessions WHERE Status=Extracted
2. Query AI Search: Get all document IDs and timestamps
3. Compare: Find missing or stale documents
4. Reindex: Queue missing/stale documents for re-embedding
```

### Triggers
- Manual: Admin API endpoint `/api/admin/reindex`
- Scheduled: Daily at 2 AM (low traffic)
- On-demand: After schema/model changes

### Backfill Strategy
- Process in batches of 100 documents
- Respect rate limits (use exponential backoff)
- Track progress in `ReindexJobs` table

---

## 6. Data Consistency Strategy

### Write Order
```
1. Save extraction to SQL (source of truth)
2. Move blob to processed/ container
3. Index to AI Search (eventually consistent)
```

### Failure Handling at Each Step

| Step | Failure | Recovery |
|------|---------|----------|
| SQL write fails | Retry with backoff, then dead-letter | Blob stays in processing/ |
| Blob move fails | Log warning, continue | Orphaned blob cleanup job |
| AI Search fails | Log warning, mark for reindex | Reconciliation job catches it |

### Synchronous Indexing (Updated)
- AI Search is indexed **synchronously** after extraction completes
- No lag between SQL write and search availability
- Reconciliation job still runs daily to catch any missed documents

```csharp
// After extraction completes
await _sqlRepository.SaveExtractionAsync(result);
await _aiSearchIndexer.IndexDocumentAsync(result);  // Synchronous - blocks until indexed
```

**Trade-off:** Slight latency increase on extraction (~500ms for indexing), but users can immediately search for new sessions.

---

## Implementation Priority

| Pattern | Priority | Phase |
|---------|----------|-------|
| Exponential backoff | High | Phase 2 |
| Idempotent job IDs | High | Phase 2 |
| Dead-letter handling | Medium | Phase 2 |
| Circuit breaker | Medium | Phase 2 |
| Reindex/reconciliation | Medium | Phase 3 |

---

## References

- [Azure SDK Retry Guidance](https://learn.microsoft.com/en-us/azure/architecture/best-practices/retry-service-specific)
- [Polly Resilience Library](https://github.com/App-vNext/Polly)
- [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/)
- [Azure Functions Retry Policies](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-error-pages)
