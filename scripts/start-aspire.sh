#!/bin/bash
# =============================================================================
# SessionSight Aspire Starter (Manual Testing)
# =============================================================================
# Starts Aspire for interactive development/testing. Does NOT run tests.
# Use run-e2e.sh for automated test runs.
#
# Usage:
#   ./scripts/start-aspire.sh
#
# After starting:
#   - API: https://localhost:7039 (fixed port)
#   - Dashboard: https://localhost:17055
#   - Frontend: cd src/SessionSight.Web && services__api__https__0=https://localhost:7039 npx vite --host
#
# Common commands while running:
#   - Test health: curl -sk https://localhost:7039/health
#   - View logs: tail -f /tmp/aspire-e2e.log
#   - Run tests: API_BASE_URL="https://localhost:7039" dotnet test tests/SessionSight.FunctionalTests
#   - Stop: Ctrl+C or pkill -9 -f "SessionSight|Aspire|dcp"
# =============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

export PATH="/home/dwight/virtualenvs/my_venv/bin:$PATH"

# Clean up any existing processes
echo "Stopping existing processes..."
pkill -9 -f "SessionSight.Api" 2>/dev/null || true
pkill -9 -f "SessionSight.AppHost" 2>/dev/null || true
pkill -9 -f "SessionSight" 2>/dev/null || true
pkill -9 -f "Aspire" 2>/dev/null || true
pkill -9 -f "dcp" 2>/dev/null || true
sleep 2

cd "$PROJECT_ROOT/src/SessionSight.AppHost"
echo ""
echo "Starting Aspire..."
echo "  API: https://localhost:7039"
echo "  Dashboard: https://localhost:17055"
echo ""
echo "To start frontend:"
echo "  cd src/SessionSight.Web && services__api__https__0=https://localhost:7039 npx vite --host"
echo ""
dotnet run
