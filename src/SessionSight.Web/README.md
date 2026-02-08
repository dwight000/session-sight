# SessionSight Web

React + TypeScript + Vite frontend for SessionSight.

## Development

```bash
# Full stack with sample data (recommended)
./scripts/start-dev.sh

# Or manually
./scripts/start-aspire.sh  # Start backend
services__api__https__0=https://localhost:7039 npx vite --host  # Start frontend
```

**Endpoints:**
- Frontend: http://localhost:5173
- API: https://localhost:7039

## Testing

```bash
npx vitest run                           # Unit tests
npx playwright test --project=chromium   # Smoke tests
./scripts/run-e2e.sh --frontend          # Full-stack E2E
```

## Structure

```
src/
├── api/          # API client functions
├── components/   # Reusable UI components
├── hooks/        # React Query hooks
├── pages/        # Route pages
└── test/         # Test fixtures and mocks
__tests__/        # Unit tests (Vitest)
e2e/              # E2E tests (Playwright)
```

See `/.claude/CLAUDE.md` for full project documentation.
