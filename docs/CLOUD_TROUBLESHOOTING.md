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

### SQL Authentication Failed (Managed Identity)

**Symptoms**: Logs show `Login failed` or `Authentication failed` for the container app identity.

**Root cause**: The Managed Identity user has not been provisioned in the SQL database. This happens when:
1. First deployment — `infra.yml` creates the AAD admin but the MI user step was skipped (container app didn't exist yet)
2. New environment — database exists but MI user was never created

**Fix**: Run `infra.yml` with `deployContainerApps=true` to ensure the Container App exists, then the "Provision Managed Identity SQL users" step will create the database user.

**Manual fix** (if `infra.yml` step fails):

```bash
ENV="dev"  # or "stage"
DB_NAME=$( [ "$ENV" = "dev" ] && echo "sessionsight" || echo "sessionsight-${ENV}" )
API_APP="sessionsight-${ENV}-api"

# Requires Azure CLI login with AAD admin credentials
sqlcmd -S "sessionsight-sql-dev.database.windows.net" -d "${DB_NAME}" -G -C \
  -Q "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '${API_APP}')
      BEGIN
        CREATE USER [${API_APP}] FROM EXTERNAL PROVIDER;
        ALTER ROLE db_owner ADD MEMBER [${API_APP}];
      END"
```

**Verify**: `curl https://sessionsight-${ENV}-api.proudsky-5508f8b0.eastus2.azurecontainerapps.io/api/therapists` should return 200.

### Azure SQL Connection Timeout (Serverless Auto-Pause)

**Symptoms**: Logs show `Connection Timeout Expired` during `post-login phase`:
```
Connection Timeout Expired. The timeout period elapsed during the post-login phase.
[Pre-Login] initialization=82; handshake=16; [Login] initialization=1; authentication=3; [Post-Login] complete=14058
```

**Root cause**: Azure SQL Serverless (free tier) auto-pauses after inactivity. First connection must "wake up" the database, taking 10-30+ seconds. Default 15s timeout is too short.

**Fix**: The connection string in `infra/main.bicep` already includes `Connection Timeout=60`. If you need to update it manually:

```bash
az containerapp update -g rg-sessionsight-dev -n sessionsight-dev-api \
  --set-env-vars "ConnectionStrings__sessionsight=Server=sessionsight-sql-dev.database.windows.net;Database=sessionsight;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
```

**Prevention**: The 60s timeout is set in `infra/main.bicep`.

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

1. Add GitHub PAT to user secrets:
   ```bash
   dotnet user-secrets set --project src/SessionSight.AppHost 'Parameters:ghcr-token' 'YOUR_GITHUB_PAT'
   ```
2. Deploy with Container Apps enabled:
   ```bash
   GHCR_TOKEN=$(dotnet user-secrets list --project src/SessionSight.AppHost | grep ghcr-token | cut -d'=' -f2 | tr -d ' ')
   USER_ID=$(az ad signed-in-user show --query id -o tsv)

   az deployment sub create --location eastus2 --template-file infra/main.bicep \
     --parameters environmentName=dev \
     --parameters developerUserObjectId="$USER_ID" \
     --parameters deployContainerApps=true \
     --parameters ghcrToken="$GHCR_TOKEN"
   ```

SQL auth uses Managed Identity — no password sync needed. The connection string contains `Authentication=Active Directory Managed Identity` with no credentials.

## Rollback Procedure

### 1. Find the Last-Good Image Tag

Image tags are 7-character SHA prefixes. Find a known-good tag from any of these sources:

```bash
# From GitHub Actions deploy run history
gh run list --workflow=deploy.yml --status=success --limit 5

# From GHCR package tags
gh api user/packages/container/sessionsight-api/versions --jq '.[].metadata.container.tags[]' | head -10

# From git log (first 7 chars of each commit SHA)
git log --oneline origin/main | head -10
```

### 2. Trigger Rollback

**GitHub Actions UI:**
1. Go to Actions → Deploy → Run workflow
2. Set **environment** to `dev` or `stage`
3. Set **rollback_tag** to the 7-character SHA (e.g., `abc1234`)
4. Click **Run workflow**

The rollback job skips the build entirely and directly updates the container images to the specified tag.

### 3. EF Migration Caveat

Image rollback does **not** reverse database schema changes. This is safe when:
- Migrations are **additive** (new tables, new columns) — old code ignores the new schema
- No migrations were applied between the rollback target and the current version

If a **destructive migration** was applied (dropped column, renamed table), manual database work is required before the rolled-back code will function correctly.

### 4. CLI Fallback

If GitHub Actions is unavailable, roll back manually:

```bash
# Login
az login

# Roll back API
az containerapp update -g rg-sessionsight-dev -n sessionsight-dev-api \
  --image ghcr.io/dwight000/sessionsight-api:<TAG>

# Roll back Web
az containerapp update -g rg-sessionsight-dev -n sessionsight-dev-web \
  --image ghcr.io/dwight000/sessionsight-web:<TAG>
```

Replace `dev` with `stage` for the stage environment.

### 5. Verify

```bash
# Confirm image tag
az containerapp show -g rg-sessionsight-dev -n sessionsight-dev-api \
  --query "properties.template.containers[0].image" -o tsv

# Health check
curl https://sessionsight-dev-api.proudsky-5508f8b0.eastus2.azurecontainerapps.io/api/patients
```

---

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
