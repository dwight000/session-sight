# Phase 6: Deployment

> **Status**: Infrastructure ready (P1-015, B-026, B-027 complete)

## Overview

Phase 6 focuses on deploying SessionSight to Azure: CI/CD pipelines, environment configuration, and release management.

## Environment Model

SessionSight uses a simple two-tier model:

| Environment | Purpose | Where It Runs |
|-------------|---------|---------------|
| **Local** | Development, debugging | Your machine (dotnet run / Aspire) |
| **Dev** | Testing, demos, portfolio | Azure Container Apps |

Both environments share the same Azure backend services (rg-sessionsight-dev):
- Azure OpenAI
- Azure SQL
- Azure AI Search
- Document Intelligence
- Key Vault

### Why No Prod Environment?

This is a solo portfolio project. A single cloud environment (`dev`) is sufficient for:
- Demonstrating the deployed application
- Running integration tests against real Azure services
- Showcasing to potential employers

A `prod` environment can be added later if needed for:
- Separate resources with different scaling/pricing
- Approval gates before deployment
- Distinct configuration (e.g., higher rate limits)

## Infrastructure Completed

| Item | Status | Details |
|------|--------|---------|
| Bicep IaC | Done (P1-015) | 9 modules, infra.yml workflow |
| GitHub Environment | Done (B-027) | `dev` environment created |
| OIDC Authentication | Done (B-026, B-027) | Federated credentials for branch + environment |
| Environment Secrets | Done (B-027) | AZURE_CLIENT_ID, TENANT_ID, SUBSCRIPTION_ID |
| Azure Resources | Done (Phase 0) | All resources in rg-sessionsight-dev |

### OIDC Federated Credentials

The `session-sight-github-actions` app registration has three federated credentials:

| Name | Subject | Use Case |
|------|---------|----------|
| `github-develop-branch` | `repo:dwight000/session-sight:ref:refs/heads/develop` | CI on develop branch |
| `github-main-branch` | `repo:dwight000/session-sight:ref:refs/heads/main` | CI on main branch |
| `github-env-dev` | `repo:dwight000/session-sight:environment:dev` | Deploy workflow with `environment: dev` |

## Goals

- Fully automated CI/CD pipeline
- Dev environment deployed and functional
- Infrastructure drift detection
- Release and rollback procedures

## Key Deliverables

### 1. Deploy Workflow

GitHub Actions workflow (`deploy.yml`) that:
- Triggers on push to main or manual dispatch
- Uses `environment: dev` for OIDC auth
- Runs `azd deploy` to deploy to Azure Container Apps

### 2. Infrastructure Management

- Hand-written Bicep IaC (P1-015 complete)
  - 9 modular templates: resourceGroup, keyVault, storage, sql, openai, search, docintell, aiHub, aiProject
  - `infra.yml` workflow with OIDC auth, what-if mode for PRs
- Drift detection: `az deployment sub what-if` against committed Bicep
- Rollback: keep previous container image tag

### 3. Release Management

- GitHub Release with SemVer tag (v1.0.0)
- Dependabot for dependency updates
- Demo data and walkthrough

## Technical Approach

- GitHub Actions for CI/CD
- GitHub environments for secrets
- Hand-written Bicep for infrastructure (`infra.yml` workflow)
- Separation of concerns:
  - `ci.yml` → Build, test, coverage
  - `infra.yml` → Azure resource deployment
  - `deploy.yml` → Application deployment (future)

## Exit Criteria

- [x] GitHub Environment `dev` exists
- [x] OIDC authentication works with environment
- [x] Infrastructure as Code (Bicep) complete
- [ ] Dev environment deployed and functional
- [ ] CI/CD pipeline runs on PR and release
- [ ] Infra drift detection working
- [ ] Rollback procedure documented and tested
- [ ] v1.0.0 release created
- [ ] Demo walkthrough complete

---

## Tasks

| ID | Task | Status |
|----|------|--------|
| B-026 | Configure GitHub OIDC auth for Azure | Done |
| B-027 | Map CI/CD secrets to GitHub environments | Done |
| P1-015 | Write Bicep IaC from scratch | Done |
| P6-001 | Configure dev environment resources | Blocked |
| P6-003 | GitHub Actions deploy.yml | Blocked |
| B-029 | Infra drift checks | Ready |
| B-031 | Rollback strategy | Blocked |
| P6-005 | Create v1.0.0 release | Blocked |
| P6-006 | Enable Dependabot | Blocked |
| P6-007 | Demo data and walkthrough | Blocked |

Note: P6-002 (prod environment) and B-030 (dev→prod promotion) are deferred. They can be added if a production environment becomes necessary.

---

## Post-Release

- Monitor for issues
- Gather feedback
- Plan v1.1.0 improvements
