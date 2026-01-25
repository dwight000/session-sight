# Blob Trigger Ingestion Specification

> **Purpose**: Define how files dropped into Azure Blob Storage automatically trigger the extraction pipeline.

## Overview

Two ingestion methods:
1. **API Upload** - Direct upload via REST endpoint (covered in phase-1)
2. **Blob Drop** - File dropped in blob container triggers processing automatically

This spec covers method #2.

---

## Architecture

```
┌──────────────────┐
│   User/System    │
│   drops file     │
└────────┬─────────┘
         │
         ▼
┌──────────────────────────────────────┐
│     Azure Blob Storage               │
│     Container: "incoming"            │
│     ─────────────────────            │
│     /patient-P001/session-001.txt    │
│     /patient-P002/session-005.pdf    │
└────────────────┬─────────────────────┘
                 │ Event Grid subscription
                 │ (BlobCreated event)
                 ▼
┌──────────────────────────────────────┐
│     Azure Function                   │
│     (Blob Trigger)                   │
│     ─────────────────────            │
│     • Validates file                 │
│     • Extracts metadata from path    │
│     • Queues processing job          │
└────────────────┬─────────────────────┘
                 │ HTTP call or Queue message
                 ▼
┌──────────────────────────────────────┐
│     Azure AI Document Intelligence   │
│     ─────────────────────            │
│     • OCR (PDF/image to text)        │
│     • Section identification         │
│     • Outputs structured Markdown    │
└────────────────┬─────────────────────┘
                 │
                 ▼
┌──────────────────────────────────────┐
│     SessionSight Agent Service        │
│     ─────────────────────            │
│     • Intake Agent (metadata)        │
│     • Extractor Agent (schema)       │
│     • Summarizer Agent               │
└────────────────┬─────────────────────┘
                 │
         ┌───────┴───────┐
         ▼               ▼
┌─────────────┐   ┌─────────────┐
│ Azure SQL   │   │ Processed   │
│ (results)   │   │ Container   │
└─────────────┘   └─────────────┘
```

---

## Blob Container Structure

```
sessionsight-storage/
├── incoming/                    # Drop files here
│   └── {patient-id}/           # Folder per patient
│       └── {filename}.txt|pdf  # Session notes
│
├── processing/                  # Files being processed
│   └── {patient-id}/
│       └── {session-id}/
│
├── processed/                   # Completed files
│   └── {patient-id}/
│       └── {session-id}/
│           ├── original.txt
│           ├── extracted.json
│           └── summary.json
│
└── failed/                      # Failed processing
    └── {patient-id}/
        └── {filename}.txt
```

---

## File Naming Convention

**Option A: Path-based metadata**
```
incoming/P001/session-2026-01-21.txt
         └─┬─┘ └──────┬──────────┘
      patient-id    filename (parsed for date)
```

**Option B: Metadata file**
```
incoming/upload-001/
├── notes.txt           # The actual note
└── metadata.json       # { "patientId": "P001", "sessionDate": "2026-01-21" }
```

**Recommended**: Option A for simplicity. Patient ID from folder, date from filename.

---

## Azure Function Implementation

### Function Definition

```csharp
public class BlobTriggerFunction
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlobTriggerFunction> _logger;

    [Function("ProcessIncomingNote")]
    public async Task Run(
        [BlobTrigger("incoming/{patientId}/{fileName}", Connection = "StorageConnection")]
        BlobClient blobClient,
        string patientId,
        string fileName,
        FunctionContext context)
    {
        _logger.LogInformation("Processing blob: {PatientId}/{FileName}", patientId, fileName);

        // 1. Validate file
        var properties = await blobClient.GetPropertiesAsync();
        if (properties.Value.ContentLength > 10_000_000) // 10MB limit
        {
            _logger.LogWarning("File too large: {Size}", properties.Value.ContentLength);
            await MoveToBlobAsync(blobClient, "failed", patientId, fileName);
            return;
        }

        // 2. Move to processing
        var processingBlob = await MoveToBlobAsync(blobClient, "processing", patientId, fileName);

        // 3. Parse metadata from path
        var sessionDate = ParseDateFromFileName(fileName);

        // 4. Call agent service
        var request = new ProcessNoteRequest
        {
            PatientId = patientId,
            BlobUri = processingBlob.Uri.ToString(),
            SessionDate = sessionDate,
            FileName = fileName
        };

        // Use internal API key (separate from external client keys)
        _httpClient.DefaultRequestHeaders.Add("X-Internal-Api-Key", _config["InternalApiKey"]);

        // Validate blob is from our storage account (prevent injection)
        if (!processingBlob.Uri.Host.EndsWith(".blob.core.windows.net") ||
            !processingBlob.Uri.Host.StartsWith("sessionsight"))
        {
            _logger.LogError("Invalid blob source: {Uri}", processingBlob.Uri);
            return;
        }

        var response = await _httpClient.PostAsJsonAsync(
            "https://sessionsight-api/api/ingestion/process",
            request);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Processing failed: {Status}", response.StatusCode);
            await MoveToBlobAsync(processingBlob, "failed", patientId, fileName);
        }
    }
}
```

### Configuration

```json
// host.json
{
  "version": "2.0",
  "extensions": {
    "blobs": {
      "maxDegreeOfParallelism": 4
    }
  }
}
```

### Internal Service Authentication

The Azure Function uses an **internal API key** to authenticate with the SessionSight API. This is separate from external client API keys.

| Key Type | Purpose | Stored In |
|----------|---------|-----------|
| External API Key | Client apps (Postman, frontend) | Key Vault, provided to clients |
| Internal API Key | Function → API communication | Key Vault, Function app settings |

The API validates:
1. `X-Internal-Api-Key` header matches configured internal key
2. Request originates from expected Azure Function (optional IP check)
3. Blob URI is from the SessionSight storage account (prevents injection)

```json
// local.settings.json
{
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "StorageConnection": "your-storage-connection-string",
    "InternalApiKey": "dev-internal-key-change-in-prod",
    "AgentServiceUrl": "https://localhost:5001"
  }
}
```

---

## API Endpoint for Processing

```csharp
[ApiController]
[Route("api/[controller]")]
public class IngestionController : ControllerBase
{
    private readonly ExtractionOrchestrator _orchestrator;
    private readonly IPatientRepository _patientRepo;
    private readonly ISessionRepository _sessionRepo;

    [HttpPost("process")]
    public async Task<ActionResult> ProcessNote([FromBody] ProcessNoteRequest request)
    {
        // 1. Find or create patient
        var patient = await _patientRepo.GetByExternalIdAsync(request.PatientId)
            ?? await _patientRepo.CreateAsync(new Patient { ExternalId = request.PatientId });

        // 2. Create session record
        var session = await _sessionRepo.CreateAsync(new Session
        {
            PatientId = patient.Id,
            SessionDate = request.SessionDate,
            Document = new SessionDocument
            {
                BlobUri = request.BlobUri,
                OriginalFileName = request.FileName,
                Status = DocumentStatus.Processing
            }
        });

        // 3. Trigger extraction pipeline (async)
        await _orchestrator.QueueProcessingAsync(session.Id);

        return Accepted(new { sessionId = session.Id });
    }
}

public record ProcessNoteRequest(
    string PatientId,
    string BlobUri,
    DateOnly SessionDate,
    string FileName
);
```

---

## Event Grid Configuration (Alternative to Blob Trigger)

For more reliable triggering, use Event Grid instead of direct blob trigger:

```bicep
resource eventGridSubscription 'Microsoft.EventGrid/eventSubscriptions@2022-06-15' = {
  name: 'blob-created-subscription'
  scope: storageAccount
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        resourceId: functionApp.id
        maxEventsPerBatch: 1
        preferredBatchSizeInKilobytes: 64
      }
    }
    filter: {
      subjectBeginsWith: '/blobServices/default/containers/incoming'
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
      ]
    }
  }
}
```

---

## Aspire Integration

```csharp
// Program.cs in SessionSight.AppHost
var builder = DistributedApplication.CreateBuilder(args);

// Storage with containers
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("incoming")
    .AddBlobs("processing")
    .AddBlobs("processed")
    .AddBlobs("failed");

// Azure Function for blob trigger
var blobFunction = builder.AddAzureFunctionsProject<Projects.SessionSight_BlobTrigger>("blob-trigger")
    .WithReference(storage)
    .WithReference(api);

// API
var api = builder.AddProject<Projects.SessionSight_Api>("api")
    .WithReference(sql)
    .WithReference(storage);
```

---

## Error Handling

| Error | Action |
|-------|--------|
| File too large | Move to `failed/`, log warning |
| Invalid format | Move to `failed/`, log error |
| Patient not found | Create patient automatically |
| Extraction fails | Move to `failed/`, retry later |
| Duplicate file | Skip if session exists, log info |

### Retry Policy

> **Note:** Blob trigger uses fixed-delay retry (simpler). OpenAI/Search calls use exponential backoff per `resilience.md`.

```csharp
// In Azure Function - fixed delay for blob trigger retries
[Function("ProcessIncomingNote")]
[FixedDelayRetry(3, "00:00:30")]  // 3 retries, 30s delay (blob trigger level)
public async Task Run(...)
```

---

## Monitoring

```csharp
// Application Insights telemetry
_telemetry.TrackEvent("BlobIngestion", new Dictionary<string, string>
{
    ["PatientId"] = patientId,
    ["FileName"] = fileName,
    ["Status"] = "Started"
});
```

**Metrics to track:**
- Files processed per hour
- Average processing time
- Failure rate
- Queue depth

---

## Demo Usage

```bash
# Upload a file via Azure CLI
az storage blob upload \
  --account-name sessionsightstorage \
  --container-name incoming \
  --name "P001/session-2026-01-21.txt" \
  --file ./sample-note.txt

# Watch the processing
az storage blob list \
  --account-name sessionsightstorage \
  --container-name processed \
  --prefix "P001/"
```

---

## Local Development & Testing

### The Problem
Azurite (local blob emulator) does NOT support Event Grid triggers. You cannot test the full blob trigger flow locally.

### Workaround Options

**Option A: Direct API Testing (Recommended for Dev)**

Skip the blob trigger entirely during local dev. Call the API directly:

```bash
# Upload document via API instead of blob drop
curl -X POST http://localhost:5001/api/sessions/{sessionId}/documents \
  -F "file=@sample-note.txt"
```

The API handles document upload → blob storage → processing queue.

**Option B: Manual Trigger Invocation**

Azure Functions can be triggered manually via HTTP:

```csharp
// Add HTTP trigger for local testing
#if DEBUG
[Function("ProcessBlobManual")]
public async Task ProcessBlobManual(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
{
    var blobPath = req.Query["path"];
    // Manually invoke blob processing logic
}
#endif
```

**Option C: Integration Test with Real Azure**

For end-to-end testing, use a dev Azure environment:

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task BlobTrigger_ProcessesUploadedDocument()
{
    // Requires AZURE_STORAGE_CONNECTION_STRING env var
    var blobClient = new BlobContainerClient(connectionString, "incoming");
    await blobClient.UploadBlobAsync("test-patient/test-session.txt", content);

    // Wait for processing (poll processed/ container)
    await WaitForProcessedBlobAsync("test-patient/test-session/extracted.json");
}
```

### Recommended Testing Strategy

| Test Type | Where | How |
|-----------|-------|-----|
| Unit tests | Local | Mock blob storage interfaces |
| API integration | Local | Direct API calls, Azurite for storage |
| Blob trigger E2E | Azure Dev | Real blob upload, real Event Grid |
| Full pipeline | Azure Dev | Upload → Trigger → Doc Intel → Extract → Search |

---

## Project Structure Addition

```
session-sight/
├── src/
│   ├── SessionSight.BlobTrigger/     # NEW: Azure Functions project
│   │   ├── BlobTriggerFunction.cs
│   │   ├── host.json
│   │   └── SessionSight.BlobTrigger.csproj
│   └── ... (existing projects)
```

---

## Phase Placement

This feature spans phases:
- **Phase 1**: Create storage containers, basic API endpoint
- **Phase 2**: Add Azure Function, connect to agent pipeline
- **Phase 3+**: Add Event Grid for reliability, monitoring

Consider implementing in **Phase 2** alongside the extraction pipeline.

---

## Related Specs

- **`docs/specs/resilience.md`** - Exponential backoff, idempotent job IDs, dead-letter handling, and reindex patterns for production-grade reliability.
