# ADR-002: Error Handling Strategy

**Status**: Accepted
**Date**: January 22, 2026

## Context

The application needs a consistent error handling strategy across:
- REST API responses
- Agent execution failures
- Azure service errors (OpenAI, SQL, AI Search)
- Validation errors

## Decision

### 1. API Error Format: RFC 7807 Problem Details

All API errors return standard Problem Details JSON:

```json
{
  "type": "https://sessionsight.dev/errors/extraction-failed",
  "title": "Extraction Failed",
  "status": 500,
  "detail": "Azure OpenAI returned 429 Too Many Requests after 3 retries",
  "instance": "/api/sessions/123/extract",
  "traceId": "abc123"
}
```

### 2. Exception Hierarchy

```
SessionSightException (base)
├── ValidationException
│   ├── SchemaValidationException
│   └── InputValidationException
├── ExtractionException
│   ├── AgentExecutionException
│   ├── ConfidenceBelowThresholdException
│   └── RiskAssessmentFailedException
├── AzureServiceException
│   ├── OpenAIException (429, 500, timeout)
│   ├── SqlException
│   └── SearchException
└── NotFoundException
    ├── SessionNotFoundException
    └── PatientNotFoundException
```

### 3. Global Exception Middleware

ASP.NET Core middleware catches all exceptions and maps to Problem Details:

```csharp
app.UseExceptionHandler(app => app.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var problem = exception switch
    {
        ValidationException ve => new ProblemDetails { Status = 400, Title = "Validation Error", Detail = ve.Message },
        NotFoundException nf => new ProblemDetails { Status = 404, Title = "Not Found", Detail = nf.Message },
        AzureServiceException ae => new ProblemDetails { Status = 503, Title = "Service Unavailable", Detail = ae.Message },
        _ => new ProblemDetails { Status = 500, Title = "Internal Error", Detail = "An unexpected error occurred" }
    };
    // Add traceId, write response...
}));
```

### 4. Logging Patterns

| Level | When |
|-------|------|
| Error | Unhandled exceptions, Azure service failures |
| Warning | Retries, low confidence scores, validation failures |
| Information | Request/response, agent execution steps |
| Debug | Prompt content, LLM responses |

**Never log PHI** - redact patient identifiers and note content in production.

### 5. Transient vs Permanent Errors

| Error Type | Retry? | Example |
|------------|--------|---------|
| Transient | Yes (3x with backoff) | 429 rate limit, network timeout, 503 |
| Permanent | No | 400 bad request, 404 not found, validation failure |

## References

- [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807)
- [ASP.NET Core Error Handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)
