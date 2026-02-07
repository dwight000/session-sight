#!/bin/bash
# =============================================================================
# Watch Frontend Tests (Interactive)
# =============================================================================
# Opens Playwright UI mode so you can visually watch and debug smoke tests.
# Click any test in the left panel to run it and see browser actions live.
#
# Usage: ./scripts/watch-frontend-tests.sh
#        ./scripts/watch-frontend-tests.sh --headed   # just watch, no UI
# =============================================================================

FRONTEND_DIR="$(cd "$(dirname "$0")/../src/SessionSight.Web" && pwd)"
cd "$FRONTEND_DIR"

if [[ "${1:-}" == "--headed" ]]; then
  echo "Running Playwright in headed mode (browser visible)..."
  npx playwright test --headed
else
  echo "Opening Playwright UI mode..."
  npx playwright test --ui
fi
