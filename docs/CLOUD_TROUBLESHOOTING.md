# Cloud Troubleshooting Guide

Troubleshooting SessionSight in Azure Container Apps.

## Log Locations

| Environment | Location | Access Method |
|-------------|----------|---------------|
| **Local** | `/tmp/sessionsight/api/api-*.log` | `tail`, `grep`, `rg` |
| **Cloud** | Log Analytics workspace | KQL queries via Portal or CLI |

## Deployed URLs

| Service | URL |
|---------|-----|
| API | https://sessionsight-dev-api.proudsky-5508f8b0.eastus2.azurecontainerapps.io |
| Web | https://sessionsight-dev-web.proudsky-5508f8b0.eastus2.azurecontainerapps.io |

## Accessing Cloud Logs

### Prerequisites

Container Apps Environment must have Log Analytics configured. Check current config:

```bash
az containerapp env show -g rg-sessionsight-dev -n sessionsight-dev-env \
  --query "properties.appLogsConfiguration"
```

If `logAnalyticsConfiguration` is null, logs aren't being collected. Enable via Bicep update or Portal.

### Portal Access

1. Azure Portal → Resource Groups → `rg-sessionsight-dev`
2. Select `sessionsight-dev-api` Container App
3. Left menu → **Monitoring** → **Log stream** (real-time) or **Logs** (KQL)

### CLI Access

```bash
# Get workspace ID
WORKSPACE=$(az containerapp env show -g rg-sessionsight-dev -n sessionsight-dev-env \
  --query "properties.appLogsConfiguration.logAnalyticsConfiguration.customerId" -o tsv)

# Run KQL query
az monitor log-analytics query -w $WORKSPACE \
  --analytics-query "ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'sessionsight-dev-api' | take 20" \
  -o table
```

## KQL Query Pack

Copy these queries into Azure Portal → Log Analytics → Logs.

### Recent Logs (Last 100)

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| project TimeGenerated, Log_s
| order by TimeGenerated desc
| take 100
```

### Recent Errors

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where Log_s contains "ERR]" or Log_s contains "Exception" or Log_s contains "error"
| project TimeGenerated, Log_s
| order by TimeGenerated desc
| take 50
```

### Extraction Pipeline Trace (by Session ID)

Replace `<session-id>` with actual GUID:

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where Log_s contains "<session-id>"
| project TimeGenerated, Log_s
| order by TimeGenerated asc
```

### Extraction Success/Failure Rate (Last 24h)

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where TimeGenerated > ago(24h)
| where Log_s contains "Extraction completed" or Log_s contains "Extraction failed"
| extend Status = iff(Log_s contains "completed", "Success", "Failed")
| summarize Count=count() by Status
| render piechart
```

### Extraction Duration P95 (Last 24h)

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where TimeGenerated > ago(24h)
| where Log_s contains "Extraction completed for session"
| parse Log_s with * "in " Duration:long "ms" *
| summarize P50=percentile(Duration,50), P95=percentile(Duration,95), P99=percentile(Duration,99), Avg=avg(Duration)
```

### Risk Guardrail Triggers

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where Log_s contains "RiskAssessor" or Log_s contains "guardrail" or Log_s contains "safety"
| project TimeGenerated, Log_s
| order by TimeGenerated desc
| take 50
```

### Q&A Usage by Complexity

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where TimeGenerated > ago(7d)
| where Log_s contains "QA complexity:"
| parse Log_s with * "complexity: " Complexity:string *
| summarize Count=count() by Complexity
| render columnchart
```

### Container Restarts/Crashes

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where Log_s contains "Starting" or Log_s contains "Shutdown" or Log_s contains "terminated"
| project TimeGenerated, Log_s
| order by TimeGenerated desc
| take 30
```

### HTTP 5xx Errors (Last Hour)

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where TimeGenerated > ago(1h)
| where Log_s contains "HTTP" and (Log_s contains " 500 " or Log_s contains " 502 " or Log_s contains " 503 ")
| project TimeGenerated, Log_s
| order by TimeGenerated desc
```

### Request Volume Over Time

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where TimeGenerated > ago(24h)
| where Log_s contains "HTTP"
| summarize Requests=count() by bin(TimeGenerated, 5m)
| render timechart
```

## Local-to-Cloud Triage Mapping

| Local Command | Cloud Equivalent |
|--------------|------------------|
| `curl localhost:7039/health` | `curl https://sessionsight-dev-api.proudsky-5508f8b0.eastus2.azurecontainerapps.io/health` |
| `tail /tmp/sessionsight/api/*.log` | KQL: Recent Logs query |
| `grep "Error" /tmp/sessionsight/api/*.log` | KQL: Recent Errors query |
| `grep "<session-id>" /tmp/sessionsight/api/*.log` | KQL: Extraction Pipeline Trace query |
| `rg "Extraction completed" /tmp/sessionsight/api/*.log` | KQL: Extraction Duration P95 query |
| Check if API is running | Portal → Container App → Overview → Running status |
| View real-time logs | Portal → Container App → Log stream |
| Restart API | Portal → Container App → Revisions → Restart |

## Common Issues

### Container Scaled to Zero (404 Errors)

**Symptoms**: API returns 404 "Container App is stopped or does not exist" even though app shows "Running" in Portal.

**Root cause**: Container Apps with `minReplicas: 0` scale to zero after ~5 min of inactivity. First request wakes it up (cold start takes 5-15 seconds).

**Triage**:
```bash
# Check replica count
az containerapp revision list -g rg-sessionsight-dev -n sessionsight-dev-api -o table
# Look at "Replicas" column - if 0, app is scaled down
```

**Solution**: Wait and retry. First request triggers scale-up. If you need always-on:
```bash
az containerapp update -g rg-sessionsight-dev -n sessionsight-dev-api --min-replicas 1
```

**Note**: SessionSight is now configured with `minReplicas: 1` for both API and Web containers to avoid cold start issues and ensure reliable internal communication.

### Web-to-API Proxy Issues (502/504 Errors)

**Symptoms**: Frontend loads but API calls fail with 502 Bad Gateway or 504 Gateway Timeout.

**Root cause**: The nginx proxy in the web container forwards `/api/` requests to the API container. Issues can occur with:
1. Internal DNS not resolving correctly
2. SSL handshake failures when proxying to HTTPS
3. API container not accessible on expected port

**Triage**:
```bash
# Check web container nginx logs
az containerapp logs show -g rg-sessionsight-dev -n sessionsight-dev-web --tail 50 | grep error

# Verify API responds directly
curl https://sessionsight-dev-api.proudsky-5508f8b0.eastus2.azurecontainerapps.io/api/patients
```

**Solution**: The web container nginx is configured to proxy to the API's external HTTPS URL with SSL verification disabled for internal trusted traffic. If issues persist, verify:
1. `API_URL` env var is set correctly
2. Both containers have `minReplicas: 1`
3. Nginx config includes `proxy_ssl_verify off`

### Logs Not Appearing in Log Analytics

**Symptoms**: KQL queries return empty results.

**Possible causes**:
1. Log Analytics not configured on Container Apps Environment
2. Log ingestion delay (wait 2-5 minutes)
3. Container not running/generating logs

**Solution**:

```bash
# Check if Log Analytics is configured
az containerapp env show -g rg-sessionsight-dev -n sessionsight-dev-env \
  --query "properties.appLogsConfiguration"

# Check container status
az containerapp show -g rg-sessionsight-dev -n sessionsight-dev-api \
  --query "properties.runningStatus"

# View real-time logs (bypasses Log Analytics)
az containerapp logs show -g rg-sessionsight-dev -n sessionsight-dev-api --follow
```

### API Returns 500 Errors

**Symptoms**: All API calls return HTTP 500.

**Triage steps**:

1. Check recent errors in logs:
   ```bash
   az containerapp logs show -g rg-sessionsight-dev -n sessionsight-dev-api --tail 50
   ```

2. Common causes:
   - Database connection string missing/invalid
   - Azure OpenAI endpoint not configured
   - Managed identity missing required roles

3. Verify environment variables are set:
   ```bash
   az containerapp show -g rg-sessionsight-dev -n sessionsight-dev-api \
     --query "properties.template.containers[0].env" -o table
   ```

### Container Keeps Restarting

**Symptoms**: Container status shows restarts, health checks failing.

**Triage**:

```bash
# Check container events
az containerapp revision list -g rg-sessionsight-dev -n sessionsight-dev-api -o table

# View startup logs
az containerapp logs show -g rg-sessionsight-dev -n sessionsight-dev-api \
  --tail 100 2>&1 | head -50
```

**Common causes**:
- Health check endpoint failing (database not connected)
- Missing required environment variables
- Container image not found

### Extraction Taking Too Long

**Symptoms**: Extractions timeout or take >5 minutes.

**Triage with KQL**:

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where TimeGenerated > ago(1h)
| where Log_s contains "Extraction"
| project TimeGenerated, Log_s
| order by TimeGenerated desc
```

**Common causes**:
- Azure OpenAI rate limiting (check for 429 errors)
- Large document causing multiple agent loops
- Search index timeout

### SQL Login Failed (Error 18456)

**Symptoms**: Logs show `Login failed for user 'sessionsightadmin'` with error number 18456.

**Root cause**: SQL admin password in Container Apps env var doesn't match the actual Azure SQL Server password. This commonly happens when `infra.yml` runs a Bicep deploy that resets the SQL server password (from Key Vault) but the container app still has the old password in `ConnectionStrings__sessionsight`.

**Common trigger**: Pushing `infra/` changes to `main` auto-triggers `infra.yml`, which runs Bicep and updates the SQL server password to the Key Vault value. The container's connection string is not updated by Bicep when `deployContainerApps=false`.

**Prevention**: As of B-076, `infra.yml` includes a "Sync SQL connection string to Container Apps" step that automatically updates the container's connection string after every Bicep deploy. This should prevent future occurrences.

**Manual fix** (if sync step fails or for ad-hoc recovery):

```bash
# Get the current correct password from Key Vault
SQL_PWD=$(az keyvault secret show --vault-name sessionsight-kv-dev --name sql-admin-password --query value -o tsv)

# Update the container's connection string to match
ENV="dev"  # or "stage"
DB_NAME=$( [ "$ENV" = "dev" ] && echo "sessionsight" || echo "sessionsight-${ENV}" )
az containerapp update -g rg-sessionsight-dev -n sessionsight-${ENV}-api \
  --set-env-vars "ConnectionStrings__sessionsight=Server=sessionsight-sql-dev.database.windows.net;Database=${DB_NAME};User Id=sessionsightadmin;Password=${SQL_PWD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
```

**Verify**: `curl https://sessionsight-${ENV}-api.proudsky-5508f8b0.eastus2.azurecontainerapps.io/api/therapists` should return 200.

**Old fix** (no longer needed — B-076 sync step prevents this automatically):

```bash
# Reset SQL server password to match container (before B-076, this was the manual fix)
SQL_PWD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')
az sql server update -g rg-sessionsight-dev -n sessionsight-sql-dev --admin-password "$SQL_PWD"
REVISION=$(az containerapp revision list -g rg-sessionsight-dev -n sessionsight-dev-api --query "[0].name" -o tsv)
az containerapp revision restart -g rg-sessionsight-dev -n sessionsight-dev-api --revision $REVISION
```

### Azure SQL Connection Timeout (Serverless Auto-Pause)

**Symptoms**: Logs show `Connection Timeout Expired` during `post-login phase`:
```
Connection Timeout Expired. The timeout period elapsed during the post-login phase.
[Pre-Login] initialization=82; handshake=16; [Login] initialization=1; authentication=3; [Post-Login] complete=14058
```

**Root cause**: Azure SQL Serverless (free tier) auto-pauses after inactivity. First connection must "wake up" the database, taking 10-30+ seconds. Default 15s timeout is too short.

**Fix**: Increase connection timeout to 60 seconds:

```bash
# Get current SQL password
SQL_PWD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')

# Update the connection string secret with longer timeout
NEW_CONN="Server=sessionsight-sql-dev.database.windows.net;Database=sessionsight;User Id=sessionsightadmin;Password=${SQL_PWD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

az containerapp secret set -g rg-sessionsight-dev -n sessionsight-dev-api \
  --secrets "sql-connection-string=$NEW_CONN"

# Restart to apply
az containerapp revision restart -g rg-sessionsight-dev -n sessionsight-dev-api \
  --revision $(az containerapp revision list -g rg-sessionsight-dev -n sessionsight-dev-api --query "[0].name" -o tsv)
```

**Prevention**: The fix is now in `infra/main.bicep` (`Connection Timeout=60`).

### Azure OpenAI Rate Limits

**Symptoms**: Logs show HTTP 429 or "rate limit exceeded".

**Triage**:

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "sessionsight-dev-api"
| where Log_s contains "429" or Log_s contains "rate limit" or Log_s contains "throttl"
| project TimeGenerated, Log_s
| order by TimeGenerated desc
| take 20
```

**Solutions**:
- Increase Azure OpenAI TPM quota in Portal
- Check for runaway agent loops generating excessive requests

## Updating Configuration

### Update Container App Secrets

Secrets (like SQL connection strings) are managed separately from the container image:

```bash
# List current secrets
az containerapp secret list -g rg-sessionsight-dev -n sessionsight-dev-api -o table

# Update a secret
az containerapp secret set -g rg-sessionsight-dev -n sessionsight-dev-api \
  --secrets "secret-name=new-value"

# Restart to apply (required for secret changes)
REVISION=$(az containerapp revision list -g rg-sessionsight-dev -n sessionsight-dev-api --query "[0].name" -o tsv)
az containerapp revision restart -g rg-sessionsight-dev -n sessionsight-dev-api --revision $REVISION
```

### Update Environment Variables

Non-secret config can be updated directly:

```bash
# View current env vars
az containerapp show -g rg-sessionsight-dev -n sessionsight-dev-api \
  --query "properties.template.containers[0].env" -o table

# Update an env var (creates new revision, auto-deploys)
az containerapp update -g rg-sessionsight-dev -n sessionsight-dev-api \
  --set-env-vars "VAR_NAME=new-value"
```

**IMPORTANT**: Always use `--set-env-vars` for incremental updates. Never use `--replace-env-vars` as it wipes ALL existing env vars.

## CI/CD and Configuration

### What Each Workflow Does

| Workflow | Trigger | What it updates | Container Apps config? |
|----------|---------|-----------------|------------------------|
| `deploy.yml` | Push to `main` (src changes) | Container images only | ❌ No - env vars preserved |
| `infra.yml` | Push with `infra/**` changes | Azure resources (SQL, OpenAI, etc.) | ❌ No - `deployContainerApps=false` |
| Manual Bicep | `az deployment sub create` | Full infrastructure | ⚠️ Only if `deployContainerApps=true` |

### Configuration Safety

Container Apps env vars and secrets are **safe** from normal CI/CD:
- The `deploy.yml` workflow only updates container images via `az containerapp update --image`
- The `infra.yml` workflow has `deployContainerApps=false` by default

### Full Bicep Deployment (When Needed)

If you need to run a full Bicep deployment with Container Apps:

1. Ensure SQL password in Key Vault matches Azure SQL Server
2. Add GitHub PAT to user secrets:
   ```bash
   dotnet user-secrets set --project src/SessionSight.AppHost 'Parameters:ghcr-token' 'YOUR_GITHUB_PAT'
   ```
3. Deploy with Container Apps enabled:
   ```bash
   SQL_PWD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')
   GHCR_TOKEN=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep ghcr-token | cut -d'=' -f2 | tr -d ' ')
   USER_ID=$(az ad signed-in-user show --query id -o tsv)

   az deployment sub create --location eastus2 --template-file infra/main.bicep \
     --parameters environmentName=dev \
     --parameters sqlAdminPassword="$SQL_PWD" \
     --parameters developerUserObjectId="$USER_ID" \
     --parameters deployContainerApps=true \
     --parameters ghcrToken="$GHCR_TOKEN"
   ```

### SQL Password Sync

The SQL admin password must match between:
- Azure SQL Server (actual password)
- Container Apps secret/env var (connection string)
- Local user secrets (for Bicep deploys)

If they get out of sync, reset the Azure SQL password:
```bash
SQL_PWD=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep sql-password | cut -d'=' -f2 | tr -d ' ')
az sql server update -g rg-sessionsight-dev -n sessionsight-sql-dev --admin-password "$SQL_PWD"
```

## Quick Reference

| Task | CLI Command |
|------|-------------|
| Health check | `curl https://sessionsight-dev-api.proudsky-5508f8b0.eastus2.azurecontainerapps.io/api/patients` |
| View live logs | `az containerapp logs show -g rg-sessionsight-dev -n sessionsight-dev-api --follow` |
| Check status | `az containerapp show -g rg-sessionsight-dev -n sessionsight-dev-api --query "properties.runningStatus"` |
| Check replicas | `az containerapp revision list -g rg-sessionsight-dev -n sessionsight-dev-api -o table` |
| Restart app | `az containerapp revision restart -g rg-sessionsight-dev -n sessionsight-dev-api --revision <name>` |
| List secrets | `az containerapp secret list -g rg-sessionsight-dev -n sessionsight-dev-api -o table` |
| Get workspace ID | `az containerapp env show -g rg-sessionsight-dev -n sessionsight-dev-env --query "properties.appLogsConfiguration.logAnalyticsConfiguration.customerId" -o tsv` |
