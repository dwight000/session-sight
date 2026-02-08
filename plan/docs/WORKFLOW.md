# SessionSight Claude Workflow

> **Instructions for Claude sessions.** Follow this workflow to maintain project continuity.

---

## Session Start Checklist

1. **Read BACKLOG.md** - Get current status and task list
2. **Check "Active Work"** section
   - If a task is listed: Resume it
   - If empty: Run Task Selection (below)
3. **Read relevant spec** - Check `docs/specs/` for implementation details
4. **Scan Session Log** - Understand recent context (last 2-3 entries)

---

## Task Selection Algorithm

When no active work exists:

```
1. Filter tasks where Status = "Ready"
2. Sort by:
   - Phase ASC (lower phases first)
   - Table order (higher in table = higher priority)
3. Pick the FIRST task that matches
4. Move task to "Active Work" section
5. Set Status = "In-Progress" in Task Table
```

### Selection Rules

- **Never skip phases** - Complete Phase N before starting Phase N+1
- **Respect dependencies** - Blocked-By must be Done before starting
- **One task at a time** - Only one item in Active Work
- **Spikes block phases** - B-001/B-025 must pass before Phase 2

---

## During Task Execution

### Before Writing Code

1. Read relevant spec file(s) in `docs/specs/`
2. Check for related ADRs in `docs/decisions/`
3. Understand the acceptance criteria

### While Working

- Update BACKLOG.md if discovering new tasks (add to table, set Status = Blocked)
- If stuck, document blockers in Session Log
- Keep changes atomic and testable

### Code Quality Standards

- Follow patterns in existing code
- 80% test coverage target
- No PII/PHI in logs (redact sensitive data)
- Use model routing: GPT-4.1-mini for extraction/risk, GPT-4.1-nano for intake/simple

---

## Session End Checklist

1. **Update Active Work**
   - If task complete: Move to Completed Tasks table
   - If not complete: Leave in Active Work with notes
2. **Update Task Table**
   - Set completed task Status = "Done"
   - Add any newly discovered tasks
   - Update Blocked-By if dependencies changed
3. **Add Session Log entry**
   - Date + brief summary of what happened
   - Keep to 1-2 sentences, highlight key outcomes
4. **Update Current Status**
   - Phase (if changed)
   - Next Action
   - Last Updated date

---

## Common Patterns

### Starting a New Phase

1. Verify previous phase exit criteria met (see phase spec)
2. Update "Current Status" section with new phase
3. Run Task Selection to pick first task

### When a Spike Fails

1. Document failure in Session Log
2. Update spike task Status = "Blocked" with notes
3. Do NOT proceed to blocked phases
4. Flag for user decision

### Adding New Tasks

```markdown
| ID | Task | Size | Phase | Status | Blocked-By |
| B-039 | [description] | M | 2 | Blocked | P2-004 |
```

- Use next available B-XXX ID for backlog items
- Use P{phase}-XXX for phase-specific items
- Set Status = "Blocked" if has dependencies
- Set Status = "Ready" if can start immediately

### Handling User Requests

1. If user requests specific work: Do it (overrides Task Selection)
2. If request creates new task: Add to backlog
3. Document deviation in Session Log

---

## Quick Reference

### Key Files

| File | Purpose | When to Read |
|------|---------|--------------|
| `BACKLOG.md` | Task tracking | Every session start |
| `PROJECT_PLAN.md` | Context & decisions | When need background |
| `docs/specs/*.md` | Implementation details | Before each task |
| `docs/decisions/*.md` | ADRs | When making design choices |

### Phase Specs

| Phase | Spec File |
|-------|-----------|
| 0 | `docs/specs/azure-setup.md` |
| 1 | `docs/specs/phase-1-foundation.md` |
| 2 | `docs/specs/phase-2-ai-extraction.md`, `blob-trigger-ingestion.md`, `agent-tool-callbacks.md`, `resilience.md` |
| 3 | `docs/specs/phase-3-summarization-rag.md` |
| 4 | `docs/specs/phase-4-risk-dashboard.md` |
| 5 | `docs/specs/phase-5-polish-testing.md` |
| 6 | `docs/specs/phase-6-deployment.md` |

### Status Flow

```
Ready --> In-Progress --> Done
            |
            v
         Blocked (if dependency discovered)
```

---

## Reminders

- **This is a portfolio project** - Quality over speed
- **Synthetic data only** - Never use real patient data
- **Microsoft Agent Framework is preview** - Document any workarounds
- **Cloud-backed dev** - Azure services required even for local dev
- **No Co-Authored-By** - Do not add `Co-Authored-By: Claude` to future commits (owner preference)
