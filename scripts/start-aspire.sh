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
#   - Dashboard: https://localhost:17055
#   - Find API port: ss -tlnp | grep SessionSight
#   - Run tests manually: API_BASE_URL="https://localhost:<PORT>" dotnet test tests/SessionSight.FunctionalTests
#
# Common commands while running:
#   - Check ports: ss -tlnp | grep SessionSight
#   - Test health: curl -sk https://localhost:7039/health
#   - View logs: tail -f /tmp/aspire-e2e.log
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
echo "Dashboard will open at https://localhost:17055"
echo "Use 'ss -tlnp | grep SessionSight' to find API ports"
echo ""
dotnet run
