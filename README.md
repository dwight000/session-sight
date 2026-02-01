# SessionSight

AI-powered clinical session analysis tool. Extracts structured data from therapy session notes using Azure AI agents, with risk flagging, multi-level summaries, and RAG-powered Q&A.

## Tech Stack

- .NET 9, C# 13
- .NET Aspire 9.x (orchestration, service defaults, local dev)
- Azure SQL (EF Core 9)
- Azure Blob Storage (document upload)
- Azure OpenAI GPT-4o / GPT-4o-mini (AI extraction — Phase 2+)
- Azure AI Search (RAG — Phase 3+)
- FluentValidation, OpenTelemetry, xunit

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Aspire local containers)
- Azure CLI (`az login` for DefaultAzureCredential)

## Build

```bash
dotnet build session-sight.sln
```

## Test

```bash
dotnet test session-sight.sln
```

With coverage:

```bash
dotnet test session-sight.sln --collect:"XPlat Code Coverage"
dotnet tool restore
dotnet reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coverage -reporttypes:Html
```

## Run (Aspire)

```bash
dotnet run --project src/SessionSight.AppHost
```

Opens the Aspire dashboard with the API, SQL Server, and Blob Storage emulator.

### Local Development Notes

See [docs/LOCAL_DEV.md](docs/LOCAL_DEV.md) for comprehensive local development documentation including:
- First-time setup (secrets, Azure CLI, endpoints)
- Finding Aspire ports
- Running migrations manually
- Troubleshooting common issues

Quick start:

```bash
# Set SQL password (first time only)
cd src/SessionSight.AppHost
dotnet user-secrets set "Parameters:sql-password" "LocalDev#2026!"

# Start Aspire (ensure az is in PATH)
dotnet run
```

## Project Structure

```
src/
  SessionSight.Core/             Domain models, enums, schema, interfaces
  SessionSight.Infrastructure/   EF Core, repositories, blob storage
  SessionSight.Api/              REST API (11 endpoints), middleware
  SessionSight.Agents/           AI agents (extraction pipeline)
  SessionSight.BlobTrigger/      Azure Function for blob ingestion
  SessionSight.AppHost/          Aspire orchestration
  SessionSight.ServiceDefaults/  OpenTelemetry, health checks, resilience
tests/
  SessionSight.Core.Tests/       Domain model + schema tests (59 tests)
  SessionSight.Api.Tests/        Controller, validator tests (46 tests)
  SessionSight.Agents.Tests/     Agent routing + service tests (87 tests)
  SessionSight.FunctionalTests/  E2E tests with Aspire (5 tests)
scripts/
  run-e2e.sh                     Automated E2E test runner
  start-aspire.sh                Manual Aspire startup
```

## API Endpoints

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

## Architecture Docs

See `plan/docs/` for full architecture documentation including:
- [Project Plan](plan/docs/PROJECT_PLAN.md)
- [Clinical Schema](plan/docs/specs/clinical-schema.md) (82 fields, 27 enums)
- [Phase Specs](plan/docs/specs/)
- [ADRs](plan/docs/decisions/)

## License

[MIT](LICENSE)
