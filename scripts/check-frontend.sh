#!/bin/bash
# =============================================================================
# Frontend Check Script
# =============================================================================
# Runs TypeScript checking, Vitest tests, and build verification for the
# React frontend. Equivalent to check-coverage.sh for the backend.
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

# 1. TypeScript
echo -e "${YELLOW}[1/3] TypeScript type check...${NC}"
if npx tsc --noEmit; then
  echo -e "${GREEN}  ✓ No type errors${NC}"
else
  echo -e "${RED}  ✗ TypeScript errors found${NC}"
  exit 1
fi
echo ""

# 2. Vitest
echo -e "${YELLOW}[2/3] Vitest unit tests...${NC}"
if npx vitest run; then
  echo -e "${GREEN}  ✓ All tests passed${NC}"
else
  echo -e "${RED}  ✗ Tests failed${NC}"
  exit 1
fi
echo ""

# 3. Build
echo -e "${YELLOW}[3/3] Production build...${NC}"
if npx vite build; then
  echo -e "${GREEN}  ✓ Build succeeded${NC}"
else
  echo -e "${RED}  ✗ Build failed${NC}"
  exit 1
fi

echo ""
echo -e "${GREEN}=== All frontend checks passed ===${NC}"
