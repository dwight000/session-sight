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
```

Replace `<sub-id>` with your Azure subscription ID.

**Why AzureOpenAI:Endpoint?** The AI Foundry SDK's `GetChatCompletionsClient()` only discovers Serverless connections, but we deploy Azure OpenAI as a Cognitive Services resource (AzureOpenAI connection). The code calls Azure OpenAI directly to work around this SDK limitation.

### 3. Start Aspire

```bash
cd src/SessionSight.AppHost
dotnet run
```

The Aspire dashboard opens in your browser showing all services.

## Finding API Ports

Aspire assigns dynamic ports to services. To find them:

```bash
# List all listening ports for SessionSight
ss -tlnp | grep SessionSight
# or
netstat -tlnp 2>/dev/null | grep SessionSight
```

You'll see two ports for the API:
- **HTTP** (lower port): e.g., 5215
- **HTTPS** (higher port): e.g., 7128

For local development, use the HTTPS port with `-k` to skip certificate verification:

```bash
# Example: Get all patients
curl -sk https://localhost:7128/api/patients
```

Alternatively, check the Aspire dashboard for the exact endpoints.

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
# Run all E2E tests with automatic Aspire startup/cleanup
./scripts/run-e2e.sh

# Or start Aspire manually for interactive testing
./scripts/start-aspire.sh
# Then in another terminal:
API_BASE_URL="https://localhost:<PORT>" dotnet test tests/SessionSight.FunctionalTests
```

### Unit Tests Only (No Azure/Docker Required)

```bash
dotnet test --filter "FullyQualifiedName!~FunctionalTests"
```

### Functional Tests (Requires Running Aspire)

1. Start Aspire (see above)
2. Find the API HTTPS port
3. Run tests with the API URL:

```bash
API_BASE_URL="https://localhost:<PORT>" dotnet test tests/SessionSight.FunctionalTests
```

### Verify AI Foundry Extraction (E2E Test)

The `Pipeline_FullExtraction_ReturnsSuccess` test verifies the complete extraction pipeline including AI Foundry → OpenAI connection.

**Prerequisites:**
- Bicep deployed with role assignments (`B-041`) and project connection (`B-042`)
- Azure CLI logged in: `az login`
- User has Cognitive Services User role on Doc Intel and OpenAI resources

**Run the extraction test:**

```bash
# Find API port first
ss -tlnp | grep SessionSight

# Run only the extraction test
API_BASE_URL="https://localhost:<PORT>" dotnet test tests/SessionSight.FunctionalTests \
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
curl -sk https://localhost:<PORT>/api/patients
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
| Test (unit only) | `dotnet test --filter "FullyQualifiedName!~FunctionalTests"` |
| Test (all) | `API_BASE_URL=https://localhost:<PORT> dotnet test` |
| Run Aspire | `dotnet run --project src/SessionSight.AppHost` |
| Check ports | `ss -tlnp \| grep SessionSight` |
| SQL password | `dotnet user-secrets list --project src/SessionSight.AppHost` |
| API secrets | `dotnet user-secrets list --project src/SessionSight.Api` |

## Secrets Inventory

| Project | Secret | Purpose |
|---------|--------|---------|
| SessionSight.AppHost | `Parameters:sql-password` | SQL Server SA password |
| SessionSight.Api | `DocumentIntelligence:Endpoint` | Azure Doc Intelligence URL |
| SessionSight.Api | `AIFoundry:ProjectEndpoint` | Azure AI Project URL (for Agents) |
| SessionSight.Api | `AzureOpenAI:Endpoint` | Azure OpenAI URL (for chat completions) |
