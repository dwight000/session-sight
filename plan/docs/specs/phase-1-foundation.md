# Phase 1: Foundation Implementation Spec

> **Goal**: Set up the project structure, domain models, database, and basic API. No AI/agents yet - just the foundational infrastructure.

## Deliverables

1. .NET solution with Aspire orchestration
2. Domain models matching Clinical Schema
3. Azure SQL database with EF Core
4. Basic CRUD API for patients and sessions
5. Azure Blob Storage integration for document upload
6. Unit tests for core functionality

---

## 1. Solution Structure

### Create .NET Solution

```bash
# Create solution directory
mkdir session-sight && cd session-sight

# Create solution file
dotnet new sln -n SessionSight

# Create projects
dotnet new classlib -n SessionSight.Core -o src/SessionSight.Core
dotnet new classlib -n SessionSight.Infrastructure -o src/SessionSight.Infrastructure
dotnet new webapi -n SessionSight.Api -o src/SessionSight.Api
dotnet new classlib -n SessionSight.Agents -o src/SessionSight.Agents
dotnet new aspire-apphost -n SessionSight.AppHost -o src/SessionSight.AppHost
dotnet new aspire-servicedefaults -n SessionSight.ServiceDefaults -o src/SessionSight.ServiceDefaults

# Add projects to solution
dotnet sln add src/SessionSight.Core
dotnet sln add src/SessionSight.Infrastructure
dotnet sln add src/SessionSight.Api
dotnet sln add src/SessionSight.Agents
dotnet sln add src/SessionSight.AppHost
dotnet sln add src/SessionSight.ServiceDefaults

# Create test projects
dotnet new xunit -n SessionSight.Core.Tests -o tests/SessionSight.Core.Tests
dotnet new xunit -n SessionSight.Api.Tests -o tests/SessionSight.Api.Tests
dotnet sln add tests/SessionSight.Core.Tests
dotnet sln add tests/SessionSight.Api.Tests
```

### Project Dependencies

```
SessionSight.AppHost
├── References: SessionSight.Api

SessionSight.Api
├── References: SessionSight.Core
├── References: SessionSight.Infrastructure
├── References: SessionSight.ServiceDefaults

SessionSight.Infrastructure
├── References: SessionSight.Core

SessionSight.Agents
├── References: SessionSight.Core
├── References: SessionSight.Infrastructure
```

---

## 2. Domain Models (SessionSight.Core)

### Folder Structure

```
SessionSight.Core/
├── Entities/
│   ├── Patient.cs
│   ├── Session.cs
│   ├── SessionDocument.cs
│   └── ExtractionResult.cs
├── Schema/
│   ├── ClinicalSchema.cs
│   ├── SessionInfo.cs
│   ├── PresentingConcerns.cs
│   ├── MoodAssessment.cs
│   ├── RiskAssessment.cs
│   ├── MentalStatusExam.cs
│   ├── Interventions.cs
│   ├── Diagnoses.cs
│   ├── TreatmentProgress.cs
│   └── NextSteps.cs
├── Enums/
│   └── (all enum definitions)
├── ValueObjects/
│   ├── ConfidenceScore.cs
│   └── SourceMapping.cs
└── Interfaces/
    ├── IPatientRepository.cs
    ├── ISessionRepository.cs
    └── IDocumentStorage.cs
```

### Key Entities

```csharp
// Patient.cs
public class Patient
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

// Therapist.cs
public class Therapist
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public string? Credentials { get; set; }  // e.g., "LCSW", "PhD", "PsyD"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

// Session.cs
public class Session
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public Guid TherapistId { get; set; }
    public Therapist Therapist { get; set; } = null!;
    public DateOnly SessionDate { get; set; }
    public SessionType SessionType { get; set; }
    public SessionModality Modality { get; set; }
    public int? DurationMinutes { get; set; }
    public int SessionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public SessionDocument? Document { get; set; }
    public ExtractionResult? Extraction { get; set; }
}

// SessionDocument.cs
public class SessionDocument
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public string OriginalFileName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? ExtractedText { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

// ExtractionResult.cs
public class ExtractionResult
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public string SchemaVersion { get; set; } = "1.0.0";
    public string ModelUsed { get; set; } = string.Empty;
    public double OverallConfidence { get; set; }
    public bool RequiresReview { get; set; }
    public DateTime ExtractedAt { get; set; }

    // JSON columns for extracted data
    public ClinicalExtraction Data { get; set; } = new();
}
```

### Clinical Schema Classes

```csharp
// ClinicalExtraction.cs - Root object
public class ClinicalExtraction
{
    public SessionInfoExtracted SessionInfo { get; set; } = new();
    public PresentingConcernsExtracted PresentingConcerns { get; set; } = new();
    public MoodAssessmentExtracted MoodAssessment { get; set; } = new();
    public RiskAssessmentExtracted RiskAssessment { get; set; } = new();
    public MentalStatusExamExtracted MentalStatusExam { get; set; } = new();
    public InterventionsExtracted Interventions { get; set; } = new();
    public DiagnosesExtracted Diagnoses { get; set; } = new();
    public TreatmentProgressExtracted TreatmentProgress { get; set; } = new();
    public NextStepsExtracted NextSteps { get; set; } = new();
    public ExtractionMetadata Metadata { get; set; } = new();
}

// Each section has extracted values with confidence
public class ExtractedField<T>
{
    public T? Value { get; set; }
    public double Confidence { get; set; }
    public SourceMapping? Source { get; set; }
}

public class SourceMapping
{
    public string Text { get; set; } = string.Empty;
    public int StartChar { get; set; }
    public int EndChar { get; set; }
    public string? Section { get; set; }
}
```

---

## 3. Infrastructure (SessionSight.Infrastructure)

### Folder Structure

```
SessionSight.Infrastructure/
├── Data/
│   ├── SessionSightDbContext.cs
│   ├── Configurations/
│   │   ├── PatientConfiguration.cs
│   │   ├── SessionConfiguration.cs
│   │   └── ...
│   └── Migrations/
├── Repositories/
│   ├── PatientRepository.cs
│   └── SessionRepository.cs
└── Storage/
    └── AzureBlobDocumentStorage.cs
```

### DbContext

```csharp
public class SessionSightDbContext : DbContext
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Therapist> Therapists => Set<Therapist>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionDocument> Documents => Set<SessionDocument>();
    public DbSet<ExtractionResult> Extractions => Set<ExtractionResult>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();  // For idempotency

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SessionSightDbContext).Assembly);

        // JSON column for extraction data (nvarchar(max) with JSON in SQL Server)
        modelBuilder.Entity<ExtractionResult>()
            .Property(e => e.Data)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<ClinicalExtraction>(v, (JsonSerializerOptions?)null)!);
    }
}
```

### NuGet Packages

```xml
<!-- SessionSight.Infrastructure.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
<PackageReference Include="Azure.Storage.Blobs" Version="12.20.0" />
```

---

## 4. API (SessionSight.Api)

### Folder Structure

```
SessionSight.Api/
├── Controllers/
│   ├── PatientsController.cs
│   ├── SessionsController.cs
│   └── DocumentsController.cs
├── DTOs/
│   ├── PatientDto.cs
│   ├── SessionDto.cs
│   └── UploadDocumentRequest.cs
├── Program.cs
└── appsettings.json
```

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/patients` | List all patients |
| GET | `/api/patients/{id}` | Get patient by ID |
| POST | `/api/patients` | Create patient |
| PUT | `/api/patients/{id}` | Update patient |
| DELETE | `/api/patients/{id}` | Delete patient |
| GET | `/api/patients/{id}/sessions` | List patient sessions |
| GET | `/api/sessions/{id}` | Get session by ID |
| POST | `/api/sessions` | Create session |
| PUT | `/api/sessions/{id}` | Update session |
| POST | `/api/sessions/{id}/document` | Upload session document |
| GET | `/api/sessions/{id}/extraction` | Get extraction result |

### Example Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientRepository _repository;

    public PatientsController(IPatientRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PatientDto>>> GetAll()
    {
        var patients = await _repository.GetAllAsync();
        return Ok(patients.Select(p => p.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientDto>> GetById(Guid id)
    {
        var patient = await _repository.GetByIdAsync(id);
        if (patient is null) return NotFound();
        return Ok(patient.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<PatientDto>> Create(CreatePatientRequest request)
    {
        var patient = request.ToEntity();
        await _repository.AddAsync(patient);
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient.ToDto());
    }
}
```

---

## 5. Aspire AppHost

```csharp
// Program.cs in SessionSight.AppHost
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure - Azure SQL (free tier)
var sql = builder.AddAzureSqlServer("sql")
    .AddDatabase("sessionsight");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()  // Local emulator for blobs
    .AddBlobs("documents");

// API
var api = builder.AddProject<Projects.SessionSight_Api>("api")
    .WithReference(sql)
    .WithReference(storage)
    .WaitFor(sql);

builder.Build().Run();
```

**Note**: Azure SQL free tier provides 32GB storage, sufficient for this portfolio project.

---

## 6. Tests

### SessionSight.Core.Tests

```csharp
public class ClinicalSchemaTests
{
    [Fact]
    public void ConfidenceScore_ShouldBeInValidRange()
    {
        var field = new ExtractedField<int> { Value = 7, Confidence = 0.95 };
        Assert.InRange(field.Confidence, 0.0, 1.0);
    }

    [Fact]
    public void RiskAssessment_ShouldFlagHighRisk()
    {
        var risk = new RiskAssessmentExtracted
        {
            SuicidalIdeation = new ExtractedField<SuicidalIdeation>
            {
                Value = SuicidalIdeation.ActiveWithPlan,
                Confidence = 0.9
            }
        };
        Assert.True(risk.IsHighRisk());
    }
}
```

### SessionSight.Api.Tests

```csharp
public class PatientsControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsPatients()
    {
        // Arrange
        var mockRepo = new Mock<IPatientRepository>();
        mockRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Patient> { new Patient { Id = Guid.NewGuid() } });

        var controller = new PatientsController(mockRepo.Object);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var patients = Assert.IsAssignableFrom<IEnumerable<PatientDto>>(okResult.Value);
        Assert.Single(patients);
    }
}
```

---

## 7. Verification Checklist

- [ ] Solution builds without errors: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] Aspire starts successfully: `dotnet run --project src/SessionSight.AppHost`
- [ ] Aspire dashboard accessible at configured port
- [ ] Azure SQL connection working
- [ ] API responds at `/swagger`
- [ ] Can create a patient via API
- [ ] Can create a session for a patient
- [ ] Can upload a document to a session

---

## 8. Cross-Cutting Concerns

### Error Handling (RFC 7807 ProblemDetails)

Use ASP.NET Core's built-in ProblemDetails for consistent error responses:

```csharp
// Program.cs
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

// In controllers, throw exceptions or return Problem()
return Problem(
    title: "Patient not found",
    detail: $"No patient with ID {id}",
    statusCode: StatusCodes.Status404NotFound
);
```

### Request Validation (FluentValidation)

```xml
<PackageReference Include="FluentValidation.AspNetCore" Version="11.*" />
```

```csharp
// CreatePatientValidator.cs
public class CreatePatientValidator : AbstractValidator<CreatePatientRequest>
{
    public CreatePatientValidator()
    {
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
    }
}

// Program.cs
builder.Services.AddValidatorsFromAssemblyContaining<CreatePatientValidator>();
builder.Services.AddFluentValidationAutoValidation();
```

### Logging (ILogger + Correlation IDs)

```csharp
// Add correlation ID middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        await next();
    }
});

// Usage in services
_logger.LogInformation("Processing session {SessionId}", sessionId);
```

Application Insights configured via Aspire automatically captures logs.

### Key Vault Access (MSI + DefaultAzureCredential)

```csharp
// Program.cs in AppHost
var keyVault = builder.AddAzureKeyVault("secrets");

var api = builder.AddProject<Projects.SessionSight_Api>("api")
    .WithReference(keyVault);

// In API startup, secrets are automatically available via configuration
var apiKey = builder.Configuration["AzureOpenAI:ApiKey"];  // From Key Vault
```

For local development, use `dotnet user-secrets` or Azure CLI login:
```bash
az login  # DefaultAzureCredential will use your Azure CLI credentials
```

### Dependency Injection (Injectable for Testing)

```csharp
// Program.cs - Service registration
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IDocumentStorage, AzureBlobDocumentStorage>();
builder.Services.AddScoped<IExtractionService, ExtractionService>();

// All services use interface-based DI for testability
public class SessionsController : ControllerBase
{
    private readonly ISessionRepository _sessionRepo;
    private readonly IDocumentStorage _storage;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionRepository sessionRepo,
        IDocumentStorage storage,
        ILogger<SessionsController> logger)
    {
        _sessionRepo = sessionRepo;
        _storage = storage;
        _logger = logger;
    }
}
```

### Database Migration Strategy

| Environment | Who Runs | Command | When |
|-------------|----------|---------|------|
| **Local dev** | Developer | `dotnet ef database update` | After pulling changes |
| **CI/CD (dev)** | GitHub Actions | `dotnet ef database update` | Before deploy |
| **CI/CD (prod)** | GitHub Actions | `dotnet ef database update` | Before deploy |

```yaml
# .github/workflows/deploy.yml
- name: Run EF Migrations
  run: |
    dotnet tool install --global dotnet-ef
    dotnet ef database update --project src/SessionSight.Infrastructure --startup-project src/SessionSight.Api
  env:
    ConnectionStrings__sessionsight: ${{ secrets.SQL_CONNECTION_STRING }}
```

**Local development:**
```bash
# Create migration
dotnet ef migrations add AddTherapistEntity --project src/SessionSight.Infrastructure --startup-project src/SessionSight.Api

# Apply migration
dotnet ef database update --project src/SessionSight.Infrastructure --startup-project src/SessionSight.Api
```

**Rollback (if needed):**
```bash
dotnet ef database update PreviousMigrationName --project src/SessionSight.Infrastructure
```

### PHI Redaction in Logs

**Rule: Never log PHI. Redact at the source.**

```csharp
// WRONG - logs PHI
_logger.LogInformation("Processing note for patient {PatientId}: {NoteContent}",
    patientId, noteContent);

// CORRECT - redacted
_logger.LogInformation("Processing note for patient {PatientHash}, length {Length}",
    HashPatientId(patientId), noteContent.Length);

// Helper
private static string HashPatientId(Guid patientId) =>
    Convert.ToBase64String(SHA256.HashData(patientId.ToByteArray()))[..8];
```

**What to redact:**
| Data | Log As |
|------|--------|
| Patient ID | Hashed: `HashPatientId(id)` |
| Patient name | Never log |
| Note content | Length only: `note.Length` |
| Extraction data | Field names only, not values |
| Therapist notes | Never log |

**App Insights configuration:**
```csharp
// Program.cs - disable request body logging
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableRequestTrackingTelemetryModule = true;
});

// Use telemetry initializer to scrub sensitive data
builder.Services.AddSingleton<ITelemetryInitializer, PhiRedactionTelemetryInitializer>();
```

---

## 9. Files to Create

| File | Purpose |
|------|---------|
| `src/SessionSight.Core/Entities/*.cs` | Domain entities |
| `src/SessionSight.Core/Schema/*.cs` | Clinical schema models |
| `src/SessionSight.Core/Enums/*.cs` | Enum definitions |
| `src/SessionSight.Core/Interfaces/*.cs` | Repository interfaces |
| `src/SessionSight.Infrastructure/Data/SessionSightDbContext.cs` | EF Core context |
| `src/SessionSight.Infrastructure/Repositories/*.cs` | Repository implementations |
| `src/SessionSight.Api/Controllers/*.cs` | API controllers |
| `src/SessionSight.Api/DTOs/*.cs` | Data transfer objects |
| `src/SessionSight.AppHost/Program.cs` | Aspire configuration |
| `tests/SessionSight.Core.Tests/*.cs` | Core unit tests |
| `tests/SessionSight.Api.Tests/*.cs` | API tests |

---

---

## Exit Criteria (Phase Gates)

Phase 1 is complete when all criteria are met:

| Metric | Target | Measurement |
|--------|--------|-------------|
| Build status | Green | `dotnet build` succeeds |
| Test pass rate | 100% | `dotnet test` all pass |
| Code coverage | 80% | Coverage report from CI |
| API health | 200 OK | `/health` endpoint responds |
| Aspire dashboard | Accessible | Manual verification |
| CRUD operations | Working | Create patient, session, upload doc via Swagger |

**Verification commands:**
```bash
dotnet build
dotnet test --collect:"XPlat Code Coverage"
curl -s http://localhost:5000/health | jq .status
```

---

## Next Phase

After Phase 1 is complete, proceed to **Phase 2: AI Extraction Pipeline** which adds:
- Azure AI Foundry integration
- Intake Agent for document parsing
- Clinical Extractor Agent
- Model routing implementation
