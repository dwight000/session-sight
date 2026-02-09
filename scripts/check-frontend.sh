#!/bin/bash
# =============================================================================
# Frontend Check Script
# =============================================================================
# Runs TypeScript checking, Vitest tests with 83% coverage threshold, Playwright
# smoke tests, and build verification. Equivalent to check-backend.sh for backend.
#
# Usage: ./scripts/check-frontend.sh
# =============================================================================
set -euo pipefail

FRONTEND_DIR="$(cd "$(dirname "$0")/../src/SessionSight.Web" && pwd)"

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

cd "$FRONTEND_DIR"

echo -e "${YELLOW}=== Frontend Checks ===${NC}"
echo ""

# 1. TypeScript (production)
echo -e "${YELLOW}[1/5] TypeScript type check...${NC}"
if npx tsc --noEmit; then
  echo -e "${GREEN}  ✓ No type errors${NC}"
else
  echo -e "${RED}  ✗ TypeScript errors found${NC}"
  exit 1
fi
echo ""

# 2. TypeScript (test files)
echo -e "${YELLOW}[2/5] Type-check test files...${NC}"
if npx tsc --noEmit --project tsconfig.test.json; then
  echo -e "${GREEN}  ✓ No test type errors${NC}"
else
  echo -e "${RED}  ✗ Test type errors found${NC}"
  exit 1
fi
echo ""

# 3. Vitest with coverage
echo -e "${YELLOW}[3/5] Vitest unit tests with coverage...${NC}"
if npx vitest run --coverage; then
  echo -e "${GREEN}  ✓ All tests passed, coverage threshold met${NC}"
else
  echo -e "${RED}  ✗ Tests failed or coverage below configured threshold${NC}"
  exit 1
fi
echo ""

# 4. Playwright smoke tests (chromium project only - fullStack requires real backend)
echo -e "${YELLOW}[4/5] Playwright smoke tests...${NC}"
if npx playwright test --project=chromium; then
  echo -e "${GREEN}  ✓ All smoke tests passed${NC}"
else
  echo -e "${RED}  ✗ Smoke tests failed${NC}"
  exit 1
fi
echo ""

# 5. Build
echo -e "${YELLOW}[5/5] Production build...${NC}"
if npx vite build; then
  echo -e "${GREEN}  ✓ Build succeeded${NC}"
else
  echo -e "${RED}  ✗ Build failed${NC}"
  exit 1
fi

echo ""
echo -e "${GREEN}=== All frontend checks passed ===${NC}"
