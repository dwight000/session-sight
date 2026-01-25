# Phase 2: AI Extraction Pipeline Spec

> **Goal**: Implement the core AI agents for document intake and clinical data extraction using Microsoft Agent Framework and Azure OpenAI.

## Prerequisites

- Phase 1 complete (solution structure, domain models, basic API)
- Azure subscription with Azure OpenAI access
- Azure AI Search resource created
- Azure AI Document Intelligence resource created (for PDF/OCR)

---

## Deliverables

1. Azure OpenAI configuration (GPT-4o, GPT-4o-mini, embeddings)
2. Document Intelligence integration (OCR, PDF parsing, section extraction)
3. Intake Agent (metadata extraction from Document Intelligence output)
4. Clinical Extractor Agent (schema extraction)
5. **Risk Assessor Agent** (safety-critical)
6. Model Router (GPT-4o / GPT-4o-mini selection)
7. **Agent-to-Tool callbacks** (key demo feature)
8. Extraction confidence scoring
9. Integration tests with synthetic notes

**Related Spec**: See `agent-tool-callbacks.md` for the tool calling pattern implementation.

---

## 1. Azure OpenAI Setup

### Required Azure Resources

```
Azure Resource Group: rg-sessionsight-dev
├── Azure OpenAI: sessionsight-openai
│   ├── Model Deployments:
│   │   ├── gpt-4o (for extraction, risk, summarization)
│   │   ├── gpt-4o-mini (for simple Q&A)
│   │   └── text-embedding-3-large (for RAG)
├── Azure AI Document Intelligence: sessionsight-docint
│   └── prebuilt-layout (OCR, section extraction)
├── Azure AI Search: sessionsight-search
└── Storage Account: sessionsightstorage (from Phase 1)
```

### Aspire Configuration Update

```csharp
// Program.cs in SessionSight.AppHost
var builder = DistributedApplication.CreateBuilder(args);

// Existing infrastructure (from Phase 1)
var sql = builder.AddAzureSqlServer("sql").AddDatabase("sessionsight");
var storage = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("documents");

// NEW: Azure OpenAI
var openai = builder.AddAzureOpenAI("openai")
    .AddDeployment(new("gpt-4o", "gpt-4o"))
    .AddDeployment(new("gpt-4o-mini", "gpt-4o-mini"))
    .AddDeployment(new("text-embedding-3-large", "text-embedding-3-large"));

// NEW: Azure AI Search
var search = builder.AddAzureSearch("search");

// Update API with AI references
var api = builder.AddProject<Projects.SessionSight_Api>("api")
    .WithReference(sql)
    .WithReference(storage)
    .WithReference(openai)
    .WithReference(search)
    .WaitFor(sql);

// NEW: Agent service
var agents = builder.AddProject<Projects.SessionSight_Agents>("agents")
    .WithReference(sql)
    .WithReference(storage)
    .WithReference(openai)
    .WithReference(search);
```

---

## 2. SessionSight.Agents Project

### Folder Structure

```
SessionSight.Agents/
├── Routing/
│   ├── IModelRouter.cs
│   ├── ModelRouter.cs
│   └── TaskClassifier.cs
├── Agents/
│   ├── ISessionSightAgent.cs
│   ├── IntakeAgent.cs
│   ├── ClinicalExtractorAgent.cs
│   └── AgentOrchestrator.cs
├── Prompts/
│   ├── IntakePrompts.cs
│   └── ExtractionPrompts.cs
├── Tools/
│   ├── DocumentParserTool.cs
│   └── SchemaValidatorTool.cs
└── Services/
    ├── ExtractionService.cs
    └── DocumentProcessingService.cs
```

### NuGet Packages

> **Warning (Jan 2026):** Microsoft Agent Framework is in **public preview**. Package names may differ:
> - Shown below: `Microsoft.Agents.AI.Hosting` (may not exist)
> - Alternatives: `Azure.AI.Agents.Persistent`, `Microsoft.Agents.AI`
> - **Run B-001 spike to verify actual packages before coding.** See B-025 compatibility gate.

```xml
<!-- SessionSight.Agents.csproj -->
<!-- Microsoft Agent Framework (preview) - VERIFY PACKAGE NAME VIA B-001 SPIKE -->
<PackageReference Include="Microsoft.Agents.AI.Hosting" Version="*-*" /> <!-- placeholder -->

<!-- Azure OpenAI integration -->
<PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
<PackageReference Include="Microsoft.Extensions.AI.AzureAIInference" Version="*-*" />
```

**References:**
- [Microsoft Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)
- [Semantic Kernel Agent Framework](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/)

---

## 3. Model Router

### Interface

```csharp
public interface IModelRouter
{
    string SelectModel(TaskType taskType);
    string SelectModel(string taskDescription, double complexityScore);
}

public enum TaskType
{
    DocumentIntake,      // GPT-4o-mini
    ClinicalExtraction,  // GPT-4o
    RiskAssessment,      // GPT-4o
    Summarization,       // GPT-4o
    SimpleQuery,         // GPT-4o-mini
    ComplexQuery         // GPT-4o
}
```

### Implementation

```csharp
public class ModelRouter : IModelRouter
{
    private readonly Dictionary<TaskType, string> _modelMap = new()
    {
        [TaskType.DocumentIntake] = "gpt-4o-mini",
        [TaskType.ClinicalExtraction] = "gpt-4o",
        [TaskType.RiskAssessment] = "gpt-4o",
        [TaskType.Summarization] = "gpt-4o",
        [TaskType.SimpleQuery] = "gpt-4o-mini",
        [TaskType.ComplexQuery] = "gpt-4o"
    };

    public string SelectModel(TaskType taskType)
    {
        return _modelMap.GetValueOrDefault(taskType, "gpt-4o");
    }

    public string SelectModel(string taskDescription, double complexityScore)
    {
        if (complexityScore > 0.7) return "gpt-4o";
        return "gpt-4o-mini";
    }
}
```

---

## 4. Document Intelligence Integration

### Purpose
Azure AI Document Intelligence handles OCR and section extraction from PDFs/images. This is the first step in the pipeline, before any AI agents.

### Why Document Intelligence (not "Pure AI")
- **Accuracy**: Dedicated OCR outperforms GPT-4o Vision for text extraction
- **Structure**: Outputs Markdown with headings, tables preserved
- **Cost**: ~$0.01/page vs higher token costs for vision models
- **Microsoft recommended**: Standard approach for Azure RAG pipelines

### Implementation

```csharp
public interface IDocumentParser
{
    Task<ParsedDocument> ParseAsync(Stream document, string fileName, CancellationToken ct = default);
}

public class DocumentIntelligenceParser : IDocumentParser
{
    private readonly DocumentIntelligenceClient _client;
    private readonly ILogger<DocumentIntelligenceParser> _logger;

    public DocumentIntelligenceParser(DocumentIntelligenceClient client, ILogger<DocumentIntelligenceParser> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ParsedDocument> ParseAsync(Stream document, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing document: {FileName}", fileName);

        // Use prebuilt-layout for structured extraction
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            document,
            cancellationToken: ct);

        var result = operation.Value;

        return new ParsedDocument
        {
            Content = result.Content,
            MarkdownContent = ConvertToMarkdown(result),
            Sections = ExtractSections(result),
            Tables = ExtractTables(result),
            Metadata = new DocumentMetadata
            {
                PageCount = result.Pages.Count,
                FileName = fileName,
                ParsedAt = DateTimeOffset.UtcNow
            }
        };
    }

    private string ConvertToMarkdown(AnalyzeResult result)
    {
        // Document Intelligence outputs structured paragraphs
        // Section headings become ## in Markdown
        var sb = new StringBuilder();
        foreach (var paragraph in result.Paragraphs)
        {
            if (paragraph.Role == ParagraphRole.SectionHeading)
                sb.AppendLine($"## {paragraph.Content}");
            else
                sb.AppendLine(paragraph.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

public class ParsedDocument
{
    public string Content { get; set; } = string.Empty;
    public string MarkdownContent { get; set; } = string.Empty;
    public List<DocumentSection> Sections { get; set; } = new();
    public List<ExtractedTable> Tables { get; set; } = new();
    public DocumentMetadata Metadata { get; set; } = new();
}
```

### NuGet Package

```xml
<PackageReference Include="Azure.AI.DocumentIntelligence" Version="1.*" />
```

### Document Validation (Pre-Processing)

Before sending to Document Intelligence, validate documents to fail fast on unsupported inputs.

**Rejection Criteria (immediate failure):**

| Condition | Action |
|-----------|--------|
| File size > 50MB | Reject with error |
| Page count > 30 pages | Reject with error |
| Password-protected PDF | Reject with error |
| Corrupt/malformed file | Reject with error |

**Review Routing (process but flag):**

| Condition | Action |
|-----------|--------|
| Handwriting detected (>30% of content) | Process but set `RequiresReview = true` |
| Low OCR confidence (<70% average) | Process but set `RequiresReview = true` |
| Non-English detected | Process but set `RequiresReview = true` |

```csharp
public class DocumentValidationService
{
    const int MaxFileSizeBytes = 50 * 1024 * 1024;  // 50MB
    const int MaxPageCount = 30;
    const double MinOcrConfidence = 0.70;

    public async Task<ValidationResult> ValidateAsync(Stream document, string fileName)
    {
        // 1. Check file size
        if (document.Length > MaxFileSizeBytes)
            return ValidationResult.Rejected("File exceeds 50MB limit");

        // 2. Quick parse to check page count and encryption
        var quickAnalysis = await _docIntelClient.AnalyzeDocumentAsync(
            WaitUntil.Completed, "prebuilt-read", document);  // Fast, cheap

        if (quickAnalysis.Value.Pages.Count > MaxPageCount)
            return ValidationResult.Rejected($"Document has {quickAnalysis.Value.Pages.Count} pages (max: {MaxPageCount})");

        // 3. Check OCR confidence
        var avgConfidence = quickAnalysis.Value.Pages
            .SelectMany(p => p.Words)
            .Average(w => w.Confidence);

        if (avgConfidence < MinOcrConfidence)
            return ValidationResult.RequiresReview($"Low OCR confidence: {avgConfidence:P0}");

        // 4. Detect handwriting (heuristic: check for handwriting styles)
        var handwritingRatio = DetectHandwritingRatio(quickAnalysis.Value);
        if (handwritingRatio > 0.3)
            return ValidationResult.RequiresReview($"Handwriting detected: {handwritingRatio:P0}");

        return ValidationResult.Valid();
    }
}
```

---

## 5. Intake Agent

### Purpose
Process the Markdown output from Document Intelligence to extract session metadata. Since Document Intelligence handles OCR and section identification, the Intake Agent focuses on:
1. Extracting session metadata (date, therapist, patient ID)
2. Classifying document type (progress note, intake, assessment)
3. Validating the document is a therapy note

### Implementation

```csharp
public class IntakeAgent : ISessionSightAgent
{
    private readonly IChatClient _chatClient;
    private readonly IModelRouter _router;
    private readonly ILogger<IntakeAgent> _logger;

    public string Name => "IntakeAgent";

    public async Task<IntakeResult> ProcessDocumentAsync(
        ParsedDocument parsedDoc,
        CancellationToken cancellationToken = default)
    {
        var modelId = _router.SelectModel(TaskType.DocumentIntake); // GPT-4o-mini

        var prompt = IntakePrompts.GetMetadataExtractionPrompt(parsedDoc.MarkdownContent);

        var response = await _chatClient.CompleteAsync(
            new ChatMessage(ChatRole.User, prompt),
            new ChatCompletionOptions { ModelId = modelId },
            cancellationToken);

        return ParseIntakeResponse(response, parsedDoc);
    }
}

public class IntakeResult
{
    public ParsedDocument Document { get; set; } = new();
    public DocumentMetadata Metadata { get; set; } = new();
    public bool IsValidTherapyNote { get; set; }
    public string? ValidationError { get; set; }
}
```

### Intake Prompt

```csharp
public static class IntakePrompts
{
    public static string GetMetadataExtractionPrompt(string markdownContent) => $"""
        You are a metadata extraction specialist for mental health therapy notes.

        The document has already been parsed (OCR complete, sections identified).
        Extract the following metadata:

        1. Session date (if present)
        2. Therapist name (if present)
        3. Patient identifier (if present, may be redacted)
        4. Document type: progress_note | intake | assessment | discharge | other
        5. Is this a valid therapy note? (yes/no with reason if no)

        Return as JSON:
        {{
            "session_date": "YYYY-MM-DD or null",
            "therapist_name": "name or null",
            "patient_id": "id or null",
            "document_type": "progress_note",
            "is_valid_therapy_note": true,
            "validation_notes": "optional notes"
        }}

        Document (Markdown):
        ---
        {markdownContent}
        ---
        """;
}
```

---

## 6. Clinical Extractor Agent

### Purpose
Extract structured data according to Clinical Schema with confidence scores.

### Implementation

```csharp
public class ClinicalExtractorAgent : ISessionSightAgent
{
    private readonly IChatClient _chatClient;
    private readonly IModelRouter _router;
    private readonly ISchemaValidator _validator;
    private readonly ILogger<ClinicalExtractorAgent> _logger;

    public string Name => "ClinicalExtractorAgent";

    public async Task<ExtractionResult> ExtractAsync(
        IntakeResult intake,
        CancellationToken cancellationToken = default)
    {
        var modelId = _router.SelectModel(TaskType.ClinicalExtraction);

        var prompt = ExtractionPrompts.GetExtractionPrompt(
            intake.CleanedText,
            ClinicalSchema.GetSchemaDefinition());

        var response = await _chatClient.CompleteAsync(
            new ChatMessage(ChatRole.User, prompt),
            new ChatCompletionOptions
            {
                ModelId = modelId,
                Temperature = 0.1f // Low temperature for consistency
            },
            cancellationToken);

        var extraction = ParseExtractionResponse(response);

        // Validate against schema
        var validationResult = _validator.Validate(extraction);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Extraction validation failed: {Errors}",
                string.Join(", ", validationResult.Errors));
        }

        return new ExtractionResult
        {
            Data = extraction,
            SchemaVersion = ClinicalSchema.Version,
            ModelUsed = modelId,
            OverallConfidence = CalculateOverallConfidence(extraction),
            RequiresReview = DetermineIfReviewNeeded(extraction)
        };
    }
}
```

### Section-Based Extraction Strategy

**Why section-based?** Extracting all 82 fields in one prompt is:
- Token-heavy (schema alone is ~2K tokens)
- Hard to maintain (one change affects entire prompt)
- Difficult to debug (which section failed?)

Instead, use **10 separate prompts** (one per schema section), executed in parallel:

| Section | Fields | Model |
|---------|--------|-------|
| SessionInfo | 10 | GPT-4o-mini |
| PresentingConcerns | 7 | GPT-4o-mini |
| MoodAssessment | 7 | GPT-4o |
| RiskAssessment | 12 | GPT-4o (safety-critical) |
| MentalStatusExam | 9 | GPT-4o |
| InterventionsUsed | 9 | GPT-4o-mini |
| TreatmentProgress | 7 | GPT-4o-mini |
| DiagnosticInfo | 6 | GPT-4o |
| NextSteps | 8 | GPT-4o-mini |
| ExtractionMeta | 7 | GPT-4o-mini |

```csharp
public class ClinicalExtractorAgent
{
    public async Task<ExtractionResult> ExtractAsync(IntakeResult intake, CancellationToken ct)
    {
        // Run all section extractions in parallel
        var tasks = new[]
        {
            ExtractSectionAsync("SessionInfo", intake, ct),
            ExtractSectionAsync("PresentingConcerns", intake, ct),
            ExtractSectionAsync("MoodAssessment", intake, ct),
            ExtractSectionAsync("RiskAssessment", intake, ct),  // Uses GPT-4o
            ExtractSectionAsync("MentalStatusExam", intake, ct),
            ExtractSectionAsync("InterventionsUsed", intake, ct),
            ExtractSectionAsync("TreatmentProgress", intake, ct),
            ExtractSectionAsync("DiagnosticInfo", intake, ct),
            ExtractSectionAsync("NextSteps", intake, ct),
            ExtractSectionAsync("SessionMeta", intake, ct)
        };

        var results = await Task.WhenAll(tasks);
        return MergeExtractions(results);
    }
}
```

### Extraction Prompt Template (Per Section)

```csharp
public static class ExtractionPrompts
{
    public static string GetSectionPrompt(string section, string noteText, string sectionSchema) => $"""
        You are a clinical data extraction specialist for mental health therapy notes.

        Extract ONLY the {section} fields from the therapy note below.

        CRITICAL RULES:
        1. Only extract information EXPLICITLY stated in the note
        2. Do NOT infer or hallucinate values
        3. If a field is not mentioned, set value to null
        4. For each extracted value, provide:
           - "value": the extracted value
           - "confidence": score 0.0-1.0
           - "sourceText": exact quote from the note

        CONFIDENCE SCORING:
        - 0.9-1.0: Explicitly stated, exact match
        - 0.7-0.89: Clearly implied or paraphrased
        - 0.5-0.69: Requires interpretation
        - <0.5: Uncertain, flag as low confidence

        SECTION SCHEMA ({section}):
        {sectionSchema}

        SESSION NOTE:
        ---
        {noteText}
        ---

        Return JSON with the {section} fields only.
        """;
}
```

---

## 7. Risk Assessor Agent

### Purpose
Separate post-extraction agent that reviews the RiskAssessment section with additional scrutiny. This is **safety-critical** - runs AFTER Clinical Extractor.

### Why Separate?
- Risk fields need specialized validation
- Can apply 0.9 confidence threshold specifically
- Catches extraction errors before they reach production
- Enables dual-checking for safety-critical fields

### Implementation

```csharp
public class RiskAssessorAgent : ISessionSightAgent
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<RiskAssessorAgent> _logger;

    public string Name => "RiskAssessorAgent";

    public async Task<RiskAssessmentResult> AssessAsync(
        ExtractionResult extraction,
        string originalNoteText,
        CancellationToken ct = default)
    {
        // 1. Check if any risk field has confidence below 0.9
        var lowConfidenceFields = GetLowConfidenceRiskFields(extraction, threshold: 0.9);

        // 2. Re-extract risk fields with focused prompt
        var reExtraction = await ReExtractRiskFieldsAsync(originalNoteText, ct);

        // 3. Compare original extraction with re-extraction
        var discrepancies = CompareExtractions(extraction.Data.RiskAssessment, reExtraction);

        // 4. Apply keyword safety net (belt-and-suspenders)
        var keywordMatches = CheckDangerKeywords(originalNoteText);

        // 5. Determine final assessment
        var result = new RiskAssessmentResult
        {
            OriginalExtraction = extraction.Data.RiskAssessment,
            ValidatedExtraction = reExtraction,
            RequiresReview = lowConfidenceFields.Any() || discrepancies.Any() || keywordMatches.Any(),
            ReviewReasons = BuildReviewReasons(lowConfidenceFields, discrepancies, keywordMatches),
            RiskLevel = DetermineOverallRisk(reExtraction)
        };

        if (result.RequiresReview)
        {
            _logger.LogWarning("Risk assessment flagged for review: {Reasons}",
                string.Join(", ", result.ReviewReasons));
        }

        return result;
    }

    private static readonly string[] DangerKeywords = new[]
    {
        "suicide", "suicidal", "kill myself", "end my life", "not worth living",
        "self-harm", "cutting", "hurt myself", "overdose",
        "homicidal", "kill", "hurt someone", "violent thoughts"
    };

    private List<string> CheckDangerKeywords(string noteText)
    {
        var matches = new List<string>();
        var lowerNote = noteText.ToLowerInvariant();

        foreach (var keyword in DangerKeywords)
        {
            if (lowerNote.Contains(keyword))
                matches.Add(keyword);
        }
        return matches;
    }
}
```

### Risk Assessor Triggering

Risk Assessor runs automatically after Clinical Extractor in the pipeline:

```
Document Intelligence → Intake Agent → Clinical Extractor → Risk Assessor → Save
                                              ↓                    ↓
                                        (all 82 fields)    (validate risk fields)
```

---

## 8. Confidence Scoring

### Scoring Logic

```csharp
public class ConfidenceCalculator
{
    // Canonical thresholds defined in docs/specs/clinical-schema.md
    private readonly Dictionary<string, double> _categoryThresholds = new()
    {
        ["risk_assessment"] = 0.9,  // Safety-critical
        ["session_info"] = 0.7,
        ["default"] = 0.6
    };

    public double CalculateOverallConfidence(ClinicalExtraction extraction)
    {
        var scores = new List<double>();

        // Weight risk assessment higher
        if (extraction.RiskAssessment.HasValues())
        {
            scores.AddRange(extraction.RiskAssessment.GetConfidenceScores()
                .Select(s => s * 1.5)); // 50% weight boost
        }

        scores.AddRange(extraction.SessionInfo.GetConfidenceScores());
        scores.AddRange(extraction.PresentingConcerns.GetConfidenceScores());
        // ... other sections

        return scores.Any() ? scores.Average() : 0.0;
    }

    public bool RequiresReview(ClinicalExtraction extraction)
    {
        // Flag for review if:
        // 1. Any risk assessment field has low confidence
        // 2. Overall confidence below threshold
        // 3. Critical fields missing

        var riskScores = extraction.RiskAssessment.GetConfidenceScores();
        if (riskScores.Any(s => s < _categoryThresholds["risk_assessment"]))
            return true;

        if (extraction.RiskAssessment.SuicidalIdeation?.Value !=
            SuicidalIdeation.None && extraction.RiskAssessment.SuicidalIdeation?.Confidence < 0.9)
            return true;

        return false;
    }
}
```

### ISchemaValidator Interface

Validates extraction results against schema rules before saving.

```csharp
public interface ISchemaValidator
{
    ValidationResult Validate(ClinicalExtraction extraction);
}

public class SchemaValidator : ISchemaValidator
{
    public ValidationResult Validate(ClinicalExtraction extraction)
    {
        var errors = new List<ValidationError>();

        // 1. Required fields check
        if (extraction.SessionInfo.SessionDate?.Value == null)
            errors.Add(new ValidationError("SessionInfo.SessionDate", "Required field missing"));

        // 2. Enum value validation
        if (extraction.RiskAssessment.SuicidalIdeation?.Value != null)
        {
            if (!Enum.IsDefined(typeof(SuicidalIdeation), extraction.RiskAssessment.SuicidalIdeation.Value))
                errors.Add(new ValidationError("RiskAssessment.SuicidalIdeation", "Invalid enum value"));
        }

        // 3. Confidence threshold check for risk fields
        var riskFields = new[]
        {
            ("SuicidalIdeation", extraction.RiskAssessment.SuicidalIdeation?.Confidence),
            ("SelfHarm", extraction.RiskAssessment.SelfHarm?.Confidence),
            ("HomicidalIdeation", extraction.RiskAssessment.HomicidalIdeation?.Confidence),
            ("RiskLevelOverall", extraction.RiskAssessment.RiskLevelOverall?.Confidence)
        };

        foreach (var (field, confidence) in riskFields)
        {
            if (confidence.HasValue && confidence.Value < 0.9)
            {
                errors.Add(new ValidationError($"RiskAssessment.{field}",
                    $"Confidence {confidence:P0} below 0.9 threshold"));
            }
        }

        // 4. Range validation (e.g., mood 1-10)
        if (extraction.MoodAssessment.SelfReportedMood?.Value is int mood && (mood < 1 || mood > 10))
            errors.Add(new ValidationError("MoodAssessment.SelfReportedMood", "Must be 1-10"));

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

public record ValidationError(string Field, string Message);
public record ValidationResult
{
    public bool IsValid { get; init; }
    public List<ValidationError> Errors { get; init; } = new();
}
```

---

## 9. Extraction Pipeline

### Pipeline Flow

```
PDF/Image → Document Intelligence → Intake Agent → Clinical Extractor → Risk Assessor → DB
              (OCR + sections)       (metadata)     (all 82 fields)    (validate risk)
```

### Orchestrator

```csharp
public class ExtractionOrchestrator
{
    private readonly IDocumentParser _documentParser;  // Document Intelligence
    private readonly IntakeAgent _intakeAgent;
    private readonly ClinicalExtractorAgent _extractorAgent;
    private readonly RiskAssessorAgent _riskAssessor;
    private readonly ISessionRepository _sessionRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public async Task<ExtractionResult> ProcessSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetWithDocumentAsync(sessionId);
        if (session?.Document is null)
            throw new InvalidOperationException("Session has no document");

        // Step 1: Document Intelligence (OCR + section extraction)
        _logger.LogInformation("Parsing document for session {SessionId}", sessionId);
        await using var docStream = await _blobStorage.GetDocumentStreamAsync(session.Document.BlobPath);
        var parsedDoc = await _documentParser.ParseAsync(docStream, session.Document.FileName, cancellationToken);

        // Step 2: Intake Agent (metadata extraction)
        _logger.LogInformation("Extracting metadata for session {SessionId}", sessionId);
        var intakeResult = await _intakeAgent.ProcessDocumentAsync(parsedDoc, cancellationToken);

        if (!intakeResult.IsValidTherapyNote)
        {
            _logger.LogWarning("Document is not a valid therapy note: {Reason}", intakeResult.ValidationError);
            throw new InvalidOperationException($"Invalid document: {intakeResult.ValidationError}");
        }

        // Step 3: Clinical Extractor Agent (schema extraction)
        _logger.LogInformation("Extracting clinical data for session {SessionId}", sessionId);
        var extractionResult = await _extractorAgent.ExtractAsync(intakeResult, cancellationToken);

        // Step 4: Risk Assessor Agent (validate risk fields)
        _logger.LogInformation("Validating risk assessment for session {SessionId}", sessionId);
        var riskResult = await _riskAssessor.AssessAsync(
            extractionResult, parsedDoc.MarkdownContent, cancellationToken);

        if (riskResult.RequiresReview)
        {
            extractionResult.RequiresReview = true;
            extractionResult.ReviewReasons.AddRange(riskResult.ReviewReasons);
        }

        // Step 5: Save result
        extractionResult.SessionId = sessionId;
        await _sessionRepository.SaveExtractionAsync(extractionResult);

        return extractionResult;
    }
}
```

### API Endpoint

```csharp
[ApiController]
[Route("api/[controller]")]
public class ExtractionController : ControllerBase
{
    private readonly ExtractionOrchestrator _orchestrator;

    [HttpPost("{sessionId:guid}")]
    public async Task<ActionResult<ExtractionResultDto>> Extract(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var result = await _orchestrator.ProcessSessionAsync(sessionId, cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<ActionResult<ExtractionResultDto>> GetExtraction(Guid sessionId)
    {
        // ... get existing extraction
    }
}
```

---

## 10. Synthetic Test Data

### Sample Notes

Create 5-10 synthetic session notes in `/data/synthetic/`:

| File | Patient | Session Type | Risk Level |
|------|---------|--------------|------------|
| `note_001.txt` | Patient A | Intake | Low |
| `note_002.txt` | Patient A | Progress | Low |
| `note_003.txt` | Patient B | Progress | Moderate (anxiety) |
| `note_004.txt` | Patient B | Crisis | High (passive SI) |
| `note_005.txt` | Patient C | Progress | Low |

### Sample Note Format

```
PROGRESS NOTE

Patient: [REDACTED]
Date: 2026-01-15
Session #: 5
Duration: 50 minutes
Modality: Telehealth (video)

PRESENTING CONCERNS:
Patient reports continued difficulty with sleep and racing thoughts.
Mood rated as 5/10, down from 6/10 last session.
Reports increased work stress related to project deadline.

MENTAL STATUS:
Appearance: Appropriate, casual dress
Affect: Anxious, mildly tearful at times
Speech: Normal rate and volume
Thought process: Linear, goal-directed
Cognition: Intact

RISK ASSESSMENT:
Denies suicidal or homicidal ideation.
Denies self-harm urges.
Safety plan not indicated at this time.

INTERVENTIONS:
- Reviewed CBT thought record from homework (partially completed)
- Practiced diaphragmatic breathing
- Introduced sleep hygiene psychoeducation
- Assigned: Continue thought records, implement sleep hygiene changes

ASSESSMENT & PLAN:
Patient continues to meet criteria for Generalized Anxiety Disorder (F41.1).
Some symptom exacerbation related to situational stressors.
Will continue weekly sessions focusing on CBT techniques.
Consider medication evaluation if symptoms persist.

Next session: 2026-01-22
```

---

## 11. Verification Checklist

- [ ] Azure OpenAI resource created with model deployments
- [ ] Azure AI Document Intelligence resource created
- [ ] Azure AI Search resource created
- [ ] Document Intelligence successfully extracts text from PDF
- [ ] Document Intelligence identifies sections (headings in Markdown output)
- [ ] Model router correctly selects GPT-4o vs GPT-4o-mini by task
- [ ] Intake agent extracts metadata from parsed document
- [ ] Extractor agent produces valid schema output
- [ ] Risk assessor agent flags safety concerns
- [ ] Confidence scores are calculated correctly
- [ ] Risk fields with confidence < 0.9 trigger `RequiresReview` flag (see ADR-004)
- [ ] Golden file tests pass for risk assessment (37 test cases)
- [ ] Full pipeline: PDF → Doc Intel → Intake → Extractor → Risk Assessor → DB
- [ ] Results saved to database correctly
- [ ] API endpoints functional
- [ ] Integration tests pass (structure validation)

---

---

## Exit Criteria (Phase Gates)

Phase 2 is complete when all criteria are met:

| Metric | Target | Measurement |
|--------|--------|-------------|
| Extraction P95 latency | <30s per note | Application Insights query |
| Risk field F1 score | >0.90 | Golden file test suite (37 cases) |
| Overall extraction F1 | >0.85 | Validation against synthetic notes |
| Cost per note | <$0.50 | Azure cost analysis (OpenAI tokens) |
| Risk threshold enforcement | 0.9 | Unit test verifies `RequiresReview` logic |
| Blob trigger idempotency | Verified | Duplicate event test (same blob, multiple triggers) |

**Verification commands:**
```bash
dotnet test --filter "Category=GoldenFile"
az monitor app-insights query --app $APP_INSIGHTS --analytics-query "requests | where name contains 'Extract' | summarize percentile(duration, 95)"
```

**Related specs:**
- `docs/specs/resilience.md` - Retry and idempotency patterns
- `docs/decisions/ADR-004-risk-validation.md` - Risk threshold rationale

---

## Next Phase

After Phase 2 is complete, proceed to **Phase 3: Summarization & RAG** which adds:
- Summarizer Agent (session, patient, practice levels)
- Azure AI Search vector indexing
- Q&A Agent with RAG
- Embedding generation (text-embedding-3-large)
