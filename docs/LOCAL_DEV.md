# Local Development Guide

Comprehensive guide for running SessionSight locally with .NET Aspire.

## Prerequisites

| Tool | Required | Notes |
|------|----------|-------|
| .NET 9 SDK | Yes | `dotnet --version` should show 9.x |
| Docker Desktop | Yes | Runs SQL Server and Azurite containers |
| Azure CLI | Yes | Required for `DefaultAzureCredential` |

### Azure CLI Setup

Azure CLI must be in your PATH for Aspire to use Azure SDK credentials:

```bash
# Option 1: Install system-wide
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Option 2: Use a Python venv with azure-cli
# Add to your shell profile or run before starting Aspire:
export PATH="/path/to/your/venv/bin:$PATH"
```

Login before starting Aspire:

```bash
az login
az account set --subscription "your-subscription-name"
```

## First-Time Setup

### 1. Set SQL Password in User Secrets

```bash
cd src/SessionSight.AppHost
dotnet user-secrets set "Parameters:sql-password" "LocalDev#2026!"
```

### 2. Configure Azure Service Endpoints

```bash
cd src/SessionSight.Api
dotnet user-secrets set "DocumentIntelligence:Endpoint" "https://sessionsight-docint-dev.cognitiveservices.azure.com/"
dotnet user-secrets set "AIFoundry:ProjectEndpoint" "https://eastus2.api.azureml.ms/agents/v1.0/subscriptions/<sub-id>/resourceGroups/rg-sessionsight-dev/providers/Microsoft.MachineLearningServices/workspaces/sessionsight-aiproject-dev"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://sessionsight-openai-dev.openai.azure.com/"
dotnet user-secrets set "AzureSearch:Endpoint" "https://sessionsight-search-dev.search.windows.net"
```

Replace `<sub-id>` with your Azure subscription ID.

**Why AzureOpenAI:Endpoint?** The AI Foundry SDK's `GetChatCompletionsClient()` only discovers Serverless connections, but we deploy Azure OpenAI as a Cognitive Services resource (AzureOpenAI connection). The code calls Azure OpenAI directly to work around this SDK limitation.

**AzureSearch:Endpoint** enables the embedding pipeline (P3-003) for semantic search. Without it, session indexing is skipped but extraction still works.

**Note:** For Azure Search to work with `DefaultAzureCredential`, your user needs the **Search Index Data Contributor** role. Deploy with your user object ID:

```bash
# Get your user object ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)

# Deploy Bicep with developer role assignment
az deployment sub create \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters environmentName=dev sqlAdminPassword=<password> developerUserObjectId=$USER_ID
```

### 3. Start Aspire

```bash
cd src/SessionSight.AppHost
dotnet run
```

The Aspire dashboard opens in your browser showing all services.

## API Endpoints

The API runs on a fixed port:
- **API**: https://localhost:7039
- **Dashboard**: https://localhost:17055

```bash
# Test API health
curl -sk https://localhost:7039/health

# Get all patients
curl -sk https://localhost:7039/api/patients
```

## Standard Log Triage (First 60 Seconds)

Use this sequence before deeper debugging:

```bash
# 1) API health
curl -sk https://localhost:7039/health

# 2) Aspire host log
tail -n 200 /tmp/sessionsight/aspire/aspire-e2e.log

# 3) Vite log (frontend runs only)
tail -n 200 /tmp/sessionsight/vite/vite-e2e.log

# 4) API structured local logs
ls -lah /tmp/sessionsight/
ls -lah /tmp/sessionsight/api/
tail -n 200 $(ls -1t /tmp/sessionsight/api/api-*.log 2>/dev/null | head -1)
```

## Request/Response Logging Toggle

Request metadata logging is enabled in local development, and body logging is disabled by default.

Config keys (`src/SessionSight.Api/appsettings.Development.json`):

```json
"RequestResponseLogging": {
  "Enabled": true,
  "LogBodies": false,
  "MaxBodyLogBytes": null
}
```

- `Enabled`: turns request/response logging middleware on/off.
- `LogBodies`: includes full request/response bodies when `true`.
- `MaxBodyLogBytes`: optional cap when body logging is on (`null` means no truncation).

Temporary local override via user-secrets:

```bash
# Enable full request/response body logging
dotnet user-secrets set --project src/SessionSight.Api "RequestResponseLogging:LogBodies" "true"

# Optional: cap logged body size
dotnet user-secrets set --project src/SessionSight.Api "RequestResponseLogging:MaxBodyLogBytes" "4096"

# Disable body logging again
dotnet user-secrets set --project src/SessionSight.Api "RequestResponseLogging:LogBodies" "false"
```

## Running Database Migrations

Migrations run automatically when the API starts. To run manually:

```bash
# Get SQL password from secrets
SQL_PASSWORD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')

# Get SQL port from Docker
SQL_PORT=$(docker port sql-b5ed496c 1433 2>/dev/null | cut -d: -f2)
# Note: Container name may vary - check with: docker ps | grep sql

# Run migrations
dotnet ef database update \
  --project src/SessionSight.Infrastructure \
  --startup-project src/SessionSight.Api \
  --connection "Server=localhost,$SQL_PORT;Database=sessionsight;User Id=sa;Password=$SQL_PASSWORD;TrustServerCertificate=true"
```

## Running Tests

### Quick E2E Test Run

```bash
# Run backend E2E tests (C# functional tests)
./scripts/run-e2e.sh

# Run frontend E2E tests (Playwright, browser + real backend)
./scripts/run-e2e.sh --frontend

# Run frontend tests with visible browser
./scripts/run-e2e.sh --frontend --headed

# Run both backend and frontend tests
./scripts/run-e2e.sh --all

# Reuse running Aspire for faster iteration
./scripts/run-e2e.sh --hot
./scripts/run-e2e.sh --frontend --hot

# Or start Aspire manually for interactive testing
./scripts/start-aspire.sh
# Then in another terminal:
API_BASE_URL="https://localhost:7039" dotnet test tests/SessionSight.FunctionalTests
```

### Unit Tests Only (No Azure/Docker Required)

```bash
dotnet test --filter "Category!=Functional"
```

### Functional Tests (Requires Running Aspire)

1. Start Aspire (see above)
2. Run tests:

```bash
API_BASE_URL="https://localhost:7039" dotnet test tests/SessionSight.FunctionalTests
```

### Verify AI Foundry Extraction (E2E Test)

The `Pipeline_FullExtraction_ReturnsSuccess` test verifies the complete extraction pipeline including AI Foundry → OpenAI connection.

**Prerequisites:**
- Bicep deployed with role assignments (`B-041`) and project connection (`B-042`)
- Azure CLI logged in: `az login`
- User has Cognitive Services User role on Doc Intel and OpenAI resources

**Run the extraction test:**

```bash
API_BASE_URL="https://localhost:7039" dotnet test tests/SessionSight.FunctionalTests \
  --filter "FullyQualifiedName~Pipeline_FullExtraction"
```

**Common failures and fixes:**

| Error | Fix |
|-------|-----|
| "No backend service configured" | Deploy Bicep: `az deployment sub create --location eastus2 --template-file infra/main.bicep --parameters environmentName=dev sqlAdminPassword=...` |
| "401 Unauthorized" | Run `az login` and verify account has Cognitive Services User role |
| "Test PDF not found" | Ensure `sample-note.pdf` exists in `tests/SessionSight.FunctionalTests/TestData/` |

## Test Data Setup

### Create Test Therapist

If the test therapist doesn't exist, create it:

```bash
# Get SQL credentials
SQL_PASSWORD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')
SQL_CONTAINER=$(docker ps --format '{{.Names}}' | grep sql)

# Insert test therapist
docker exec $SQL_CONTAINER /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_PASSWORD" -C -d sessionsight \
  -Q "IF NOT EXISTS (SELECT 1 FROM Therapists WHERE Id = '00000000-0000-0000-0000-000000000001')
      INSERT INTO Therapists (Id, Name, LicenseNumber, Credentials, IsActive, CreatedAt)
      VALUES ('00000000-0000-0000-0000-000000000001', 'Test Therapist', 'LIC-001', 'PhD', 1, GETUTCDATE())"
```

## Troubleshooting

Start with the **Standard Log Triage** section above before issue-specific troubleshooting.

### Problem: Azure CLI not in PATH

**Symptoms**: API fails to connect to Azure services with credential errors.

**Solution**: Start Aspire with Azure CLI in PATH:

```bash
export PATH="/path/to/your/venv/bin:$PATH"
dotnet run --project src/SessionSight.AppHost
```

### Problem: Cannot connect to SQL Server

**Symptoms**: `Cannot connect to localhost` errors.

**Solution**:

1. Check if container is running:
   ```bash
   docker ps | grep sql
   ```

2. Find the correct port:
   ```bash
   docker port <container-name> 1433
   ```

3. If container doesn't exist, restart Aspire to recreate it.

### Problem: HTTPS certificate errors

**Symptoms**: `curl` or browser shows certificate warnings.

**Solution**: Use `-k` flag with curl, or access the Aspire dashboard first to accept the dev certificate:

```bash
curl -sk https://localhost:7039/api/patients
```

### Problem: Migrations not applied

**Symptoms**: Tables don't exist, API returns 500 errors.

**Solution**: Run migrations manually (see above) or restart the API service.

### Problem: Password mismatch

**Symptoms**: SQL connection fails with authentication errors.

**Solution**: Reset containers and recreate with correct password:

```bash
docker rm -f $(docker ps -aq --filter "name=sql-")
dotnet run --project src/SessionSight.AppHost
```

### Problem: Document Intelligence not configured

**Symptoms**: Document upload returns 500 error about missing endpoint.

**Solution**: Set the Document Intelligence endpoint in user secrets (see First-Time Setup).

### Problem: AI Foundry extraction fails

**Symptoms**: Extraction returns "No backend service configured" error.

**Solution**:
1. Ensure you're logged in with `az login`
2. Verify the AI Project connection exists in Azure Portal
3. Check that your user has Cognitive Services User role on both Doc Intelligence and OpenAI resources

## Quick Reference

| Task | Command |
|------|---------|
| Build | `dotnet build session-sight.sln` |
| Test (unit only) | `dotnet test --filter "Category!=Functional"` |
| Test (all) | `API_BASE_URL=https://localhost:7039 dotnet test` |
| Backend coverage (83%) | `./scripts/check-backend.sh` |
| Frontend coverage (83%) | `./scripts/check-frontend.sh` |
| Run Aspire | `dotnet run --project src/SessionSight.AppHost` |
| API endpoint | `https://localhost:7039` (fixed port) |
| SQL password | `dotnet user-secrets list --project src/SessionSight.AppHost` |
| API secrets | `dotnet user-secrets list --project src/SessionSight.Api` |

## Secrets Inventory

| Project | Secret | Purpose |
|---------|--------|---------|
| SessionSight.AppHost | `Parameters:sql-password` | SQL Server SA password |
| SessionSight.Api | `DocumentIntelligence:Endpoint` | Azure Doc Intelligence URL |
| SessionSight.Api | `AIFoundry:ProjectEndpoint` | Azure AI Project URL (for Agents) |
| SessionSight.Api | `AzureOpenAI:Endpoint` | Azure OpenAI URL (for chat completions) |
| SessionSight.Api | `AzureSearch:Endpoint` | Azure AI Search URL (for embedding/RAG) |
