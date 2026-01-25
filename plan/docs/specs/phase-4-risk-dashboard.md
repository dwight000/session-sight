# Phase 4: Risk Assessment & Dashboard

> **Status**: Placeholder - to be detailed when Phase 3 nears completion

## Overview

Phase 4 focuses on the supervisor experience: reviewing flagged sessions, monitoring patient risk trends, and providing a minimal dashboard UI.

## Goals

- Supervisor review queue for flagged sessions
- Risk trend visualization
- Patient history dashboard
- Minimal frontend (API-first, but usable UI)

## Key Deliverables

1. **Supervisor Review Queue**
   - List of sessions flagged for review (low confidence, risk indicators)
   - Ability to approve/reject extractions
   - Add supervisor notes

2. **Risk Dashboard**
   - Patients with active risk flags
   - Risk trend over time (per patient)
   - Aggregate practice metrics

3. **Patient History View**
   - Timeline of sessions
   - Extraction summaries
   - Quick access to source documents

## Technical Approach

*To be determined - options include:*
- Blazor Server (stays in .NET ecosystem)
- Simple Razor Pages
- Static SPA with API calls

## Dependencies

- Phase 3 complete (Summarization & RAG)
- Risk Assessor Agent functional (Phase 2)
- Q&A Agent functional (Phase 3)

## Exit Criteria

- [ ] Supervisor can view flagged sessions queue
- [ ] Supervisor can approve/dismiss flags
- [ ] Basic risk trend visualization works
- [ ] Patient history timeline renders correctly

---

## Tasks

See `docs/BACKLOG.md` for detailed task tracking (P4-xxx items).

---

## Next Phase

After Phase 4 is complete, proceed to **Phase 5: Polish & Testing**.
