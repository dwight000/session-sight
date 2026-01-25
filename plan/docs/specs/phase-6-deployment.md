# Phase 6: Deployment

> **Status**: Placeholder - to be detailed when Phase 5 nears completion

## Overview

Phase 6 focuses on production deployment: CI/CD pipelines, environment configuration, and release management.

## Goals

- Fully automated CI/CD pipeline
- Dev and prod environment configuration
- Infrastructure drift detection
- Release and rollback procedures

## Key Deliverables

1. **Environments**
   - Dev environment (development Azure resources)
   - Prod environment (production Azure resources)
   - Environment-specific configuration

2. **CI/CD Pipeline**
   - GitHub Actions deploy.yml (azd deploy)
   - Infrastructure drift checks
   - Dev → prod promotion with approval gates
   - Rollback strategy

3. **Release Management**
   - GitHub Release with SemVer tag (v1.0.0)
   - Dependabot for dependency updates
   - Demo data and walkthrough

## Technical Approach

- `azd` for deployment orchestration
- GitHub Actions for CI/CD
- GitHub environments for secrets/approvals
- Bicep for infrastructure (exported via `azd infra synth`)

## Exit Criteria

- [ ] Dev environment deployed and functional
- [ ] Prod environment deployed and functional
- [ ] CI/CD pipeline runs on PR and release
- [ ] Infra drift detection working
- [ ] Rollback procedure documented and tested
- [ ] v1.0.0 release created
- [ ] Demo walkthrough complete

---

## Tasks

See `docs/BACKLOG.md` for detailed task tracking (P6-xxx items).

---

## Post-Release

- Monitor for issues
- Gather feedback
- Plan v1.1.0 improvements
