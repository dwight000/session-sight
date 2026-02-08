# Phase 0: Azure & GitHub Setup Guide

> **Purpose**: Step-by-step guide to set up GitHub repo and provision Azure resources for SessionSight development.

## Prerequisites

- GitHub account
- Azure subscription (free tier eligible)
- Azure CLI installed and authenticated (`az login`)
- GitHub CLI installed and authenticated (`gh auth login`)
- .NET 9 SDK installed

---

## 1. GitHub Repository Setup

### Create Private Repository

```bash
# Create the repo (private by default)
gh repo create session-sight --private --description "AI-powered clinical notes analysis" --clone
cd session-sight

# Initialize with basic structure
mkdir -p docs/specs docs/decisions docs/research src tests data/synthetic
touch README.md .gitignore

# Initial commit
git add .
git commit -m "Initial project structure"
git push -u origin main
```

### Private → Public Timeline

| Phase | Repo Status | Reason |
|-------|-------------|--------|
| 0-1 | **Private** | Setting up secrets, infrastructure, may have config mistakes |
| 2+ | **Public** | Core functionality working, safe to share |

**Before making public, verify:**
- [ ] No secrets in git history (use `git log -p | grep -i password`)
- [ ] `.gitignore` properly excludes `appsettings.*.json`, `.env`, `*.pfx`
- [ ] No hardcoded connection strings in code
- [ ] README has proper setup instructions

### Make Public (after Phase 1)

```bash
gh repo edit session-sight --visibility public
```

---

## 2. Required Azure Resources

| Resource | SKU/Tier | Purpose | Monthly Cost |
|----------|----------|---------|--------------|
| Azure SQL | Free tier | Structured data | $0 |
| Azure OpenAI | Pay-as-you-go | GPT-4o, embeddings | Usage-based (~$5-20/mo) |
| Azure AI Search | Free tier | Vector search | $0 |
| Azure AI Document Intelligence | Pay-as-you-go | PDF/OCR, section extraction | ~$10/1K pages |
| Storage Account | Standard | Blob storage (emulated locally) | ~$0 |

---

## 3. Quick Setup with Azure CLI

### 3.1 Set Variables

```bash
# Set your values
RESOURCE_GROUP="rg-sessionsight-dev"
LOCATION="eastus2"  # Verified: OpenAI, AI Search, Document Intelligence all available
SQL_SERVER_NAME="sessionsight-sql-$RANDOM"
SEARCH_NAME="sessionsight-search-$RANDOM"
OPENAI_NAME="sessionsight-openai-$RANDOM"
DOCINT_NAME="sessionsight-docint-$RANDOM"
```

### 3.2 Create Resource Group

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

### 3.3 Create Azure SQL (Free Tier)

```bash
# Create SQL Server
az sql server create \
  --name $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user sessionsightadmin \
  --admin-password "YourSecurePassword123!"

# Create database with free tier
az sql db create \
  --name sessionsight \
  --server $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --edition GeneralPurpose \
  --compute-model Serverless \
  --family Gen5 \
  --capacity 1 \
  --free-limit-exhaustion-behavior AutoPause

# Allow Azure services
az sql server firewall-rule create \
  --server $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Allow your IP (for local dev)
MY_IP=$(curl -s ifconfig.me)
az sql server firewall-rule create \
  --server $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --name AllowMyIP \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP
```

### 3.4 Create Azure AI Search (Free Tier)

```bash
az search service create \
  --name $SEARCH_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku free
```

### 3.5 Create Azure OpenAI Resource

```bash
# Create Azure OpenAI resource
az cognitiveservices account create \
  --name $OPENAI_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --kind OpenAI \
  --sku S0

# Check available model versions first:
az cognitiveservices account list-models \
  --name $OPENAI_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[?name=='gpt-4o'].{name:name, version:version}" -o table

# Deploy GPT-4o model (check version availability above, use latest)
az cognitiveservices account deployment create \
  --name $OPENAI_NAME \
  --resource-group $RESOURCE_GROUP \
  --deployment-name gpt-4o \
  --model-name gpt-4o \
  --model-version "2024-08-06" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name Standard

# Deploy GPT-4o-mini model
az cognitiveservices account deployment create \
  --name $OPENAI_NAME \
  --resource-group $RESOURCE_GROUP \
  --deployment-name gpt-4o-mini \
  --model-name gpt-4o-mini \
  --model-version "2024-07-18" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name Standard

# Deploy embedding model
az cognitiveservices account deployment create \
  --name $OPENAI_NAME \
  --resource-group $RESOURCE_GROUP \
  --deployment-name text-embedding-3-large \
  --model-name text-embedding-3-large \
  --model-version "1" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name Standard
```

> **Note:** Model versions change frequently. If deployment fails with "model version not found", run the `list-models` command above and use the latest available version.

**Deployments created:**
- `gpt-4.1-mini` - For extraction, risk assessment, complex Q&A (~85% cheaper than GPT-4o)
- `gpt-4.1-nano` - For intake, summarization, simple Q&A (ultra-low cost)
- `text-embedding-3-large` - For RAG embeddings

### 3.6 Create Azure AI Document Intelligence

```bash
# Create Document Intelligence resource
az cognitiveservices account create \
  --name $DOCINT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --kind FormRecognizer \
  --sku S0
```

**Capabilities used:**
- `prebuilt-layout` - Extracts text, sections, tables from PDFs
- Outputs Markdown for easy downstream processing
- Handles typed therapy notes with high accuracy

---

## Get Connection Strings

```bash
# SQL Connection String
az sql db show-connection-string \
  --server $SQL_SERVER_NAME \
  --name sessionsight \
  --client ado.net

# Azure OpenAI Endpoint and Key
az cognitiveservices account show \
  --name $OPENAI_NAME \
  --resource-group $RESOURCE_GROUP \
  --query properties.endpoint

az cognitiveservices account keys list \
  --name $OPENAI_NAME \
  --resource-group $RESOURCE_GROUP

# AI Search Admin Key (for index management - create/update indexes)
az search admin-key show \
  --service-name $SEARCH_NAME \
  --resource-group $RESOURCE_GROUP

# AI Search Query Key (for searches - use this in application code)
az search query-key list \
  --service-name $SEARCH_NAME \
  --resource-group $RESOURCE_GROUP

# Which key to use:
# - AdminKey: Creating indexes, updating schema (Phase 3 setup)
# - QueryKey: All search operations (production use)

# Document Intelligence Endpoint and Key
az cognitiveservices account show \
  --name $DOCINT_NAME \
  --resource-group $RESOURCE_GROUP \
  --query properties.endpoint

az cognitiveservices account keys list \
  --name $DOCINT_NAME \
  --resource-group $RESOURCE_GROUP
```

---

## Configure Application

Create `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "sessionsight": "Server=tcp:{sql-server}.database.windows.net,1433;Database=sessionsight;User ID=sessionsightadmin;Password={password};Encrypt=True;",
    "AzureAISearch": "https://{search-name}.search.windows.net"
  },
  "AzureOpenAI": {
    "Endpoint": "https://{openai-name}.openai.azure.com/",
    "ApiKey": "{api-key}",
    "DeploymentGpt41Mini": "gpt-4.1-mini",
    "DeploymentGpt41Nano": "gpt-4.1-nano",
    "DeploymentEmbedding": "text-embedding-3-large"
  },
  "AzureAISearch": {
    "AdminKey": "{admin-key}",
    "QueryKey": "{query-key}"
  },
  "AzureDocumentIntelligence": {
    "Endpoint": "https://{docint-name}.cognitiveservices.azure.com/",
    "ApiKey": "{api-key}"
  }
}
```

Or use User Secrets for sensitive values:

```bash
cd src/SessionSight.Api
dotnet user-secrets set "ConnectionStrings:sessionsight" "Server=tcp:..."
dotnet user-secrets set "AzureOpenAI:ApiKey" "..."
```

---

## Verify Setup

```bash
# Test SQL connection
az sql db show \
  --name sessionsight \
  --server $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP

# Test Search service
az search service show \
  --name $SEARCH_NAME \
  --resource-group $RESOURCE_GROUP

# List resources in group
az resource list \
  --resource-group $RESOURCE_GROUP \
  --output table
```

---

## Cost Management

### Free Tier Limits

| Resource | Free Tier Limits |
|----------|------------------|
| Azure SQL | 32GB storage, 100K vCore seconds/month |
| AI Search | 50MB storage, 10K documents |
| Azure OpenAI | Pay per token (no free tier) |
| Document Intelligence | Pay per page (no free tier) |

### Cost Per Extraction (Estimated)

| Component | Usage | Unit Cost | Cost |
|-----------|-------|-----------|------|
| Document Intelligence | 1-3 pages | $0.01/page | ~$0.02 |
| GPT-4.1-mini (extraction) | ~5K input, ~2K output | $0.0004/1K in, $0.0016/1K out | ~$0.005 |
| GPT-4.1-nano (intake) | ~1K in/out | $0.0001/1K in, $0.0004/1K out | ~$0.0005 |
| Embeddings | ~500 tokens | $0.00013/1K | ~$0.0001 |
| **Total per extraction** | | | **~$0.03** |

**Monthly estimates:**
- 100 notes/month = ~$3
- 500 notes/month = ~$15
- 1000 notes/month = ~$30

Well under the $0.50/note SLO target.

### Cost Alerts (Set These!)

```bash
# Set budget alert at $50/month
az consumption budget create \
  --budget-name "sessionsight-budget" \
  --amount 50 \
  --resource-group $RESOURCE_GROUP \
  --time-grain Monthly \
  --category Cost
```

**Tips:**
- Use GPT-4.1-nano for development/testing (ultra-low cost)
- Free tiers are sufficient for portfolio demo
- Monitor OpenAI usage in Azure Portal → Cost Analysis

---

## Cleanup (When Done)

```bash
# Delete all resources
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| SQL connection refused | Check firewall rules, ensure your IP is allowed |
| Azure OpenAI 401 | Verify API key, check model deployment status |
| Search 403 | Verify admin key, check service status |
| Model not available | Check region supports GPT-4o (most regions do) |
