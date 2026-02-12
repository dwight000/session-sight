#!/bin/bash
# =============================================================================
# SessionSight Load Test Runner
# =============================================================================
# Runs k6 load tests against the local API.
#
# Usage:
#   ./scripts/load-test.sh                          # Cheap endpoints only
#   LOAD_TEST_EXPENSIVE=true ./scripts/load-test.sh # Include LLM endpoints
#
# Prerequisites:
#   - k6 installed (brew install k6 / apt install k6)
#   - API running (./scripts/start-dev.sh)
#   - Sample data seeded (start-dev.sh does this)
#
# Thresholds:
#   Cheap:     P95 < 500ms, error rate < 1%
#   Expensive: P95 < 120s,  error rate < 5%
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
API_URL="${API_URL:-https://localhost:7039}"
EXPENSIVE="${LOAD_TEST_EXPENSIVE:-false}"

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
NC='\033[0m'

log() { echo -e "${GREEN}[LOAD]${NC} $1"; }
warn() { echo -e "${YELLOW}[LOAD]${NC} $1"; }
error() { echo -e "${RED}[LOAD]${NC} $1" >&2; }

# -----------------------------------------------------------------------------
# Check prerequisites
# -----------------------------------------------------------------------------

# Check k6 is installed
if ! command -v k6 &> /dev/null; then
    error "k6 is not installed."
    echo ""
    echo "Install with:"
    echo "  macOS:  brew install k6"
    echo "  Ubuntu: sudo gpg -k && sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69 && echo 'deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main' | sudo tee /etc/apt/sources.list.d/k6.list && sudo apt-get update && sudo apt-get install k6"
    echo "  Other:  https://grafana.com/docs/k6/latest/set-up/install-k6/"
    exit 1
fi

log "k6 found: $(k6 version | head -1)"

# Check API is running
log "Checking API health at $API_URL..."
if ! curl -sk "$API_URL/health" 2>/dev/null | grep -q "Healthy"; then
    error "API is not running or not healthy at $API_URL"
    echo ""
    echo "Start the API first:"
    echo "  ./scripts/start-dev.sh"
    exit 1
fi

log "API is healthy"

# -----------------------------------------------------------------------------
# Run load test
# -----------------------------------------------------------------------------

cd "$PROJECT_ROOT"

if [ "$EXPENSIVE" = "true" ]; then
    warn "Running with EXPENSIVE scenarios enabled (LLM endpoints)"
    warn "This will incur LLM costs (~\$0.10)"
    echo ""
else
    log "Running cheap scenarios only (fast GET endpoints)"
    log "Set LOAD_TEST_EXPENSIVE=true to include LLM endpoints"
    echo ""
fi

log "Starting k6 load test..."
echo ""

k6 run \
    --env API_URL="$API_URL" \
    --env EXPENSIVE="$EXPENSIVE" \
    tests/load/smoke.js

RESULT=$?

echo ""
if [ $RESULT -eq 0 ]; then
    log "Load test passed! All thresholds met."
else
    error "Load test failed. Check thresholds above."
fi

exit $RESULT
