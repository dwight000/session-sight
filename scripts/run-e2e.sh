#!/bin/bash
# =============================================================================
# SessionSight E2E Test Runner
# =============================================================================
# Runs functional tests against a fresh Aspire instance with clean database.
#
# Usage:
#   ./scripts/run-e2e.sh           # Fresh database (removes SQL container)
#   ./scripts/run-e2e.sh --keep-db # Keep existing database
#
# What it does:
#   1. Kills existing SessionSight/Aspire/dcp processes
#   2. Removes old SQL container (unless --keep-db)
#   3. Starts Aspire (builds solution if needed)
#   4. Polls /health endpoint until API is ready
#   5. Discovers HTTPS port via HTTP->HTTPS redirect
#   6. Runs EF migrations and inserts test therapist
#   7. Runs functional tests with API_BASE_URL set
#
# Troubleshooting:
#   - If tests fail, check /tmp/aspire-e2e.log for Aspire output
#   - If port discovery fails, try: ss -tlnp | grep SessionSight
#   - If migrations fail, ensure SQL container is running: docker ps | grep sql
#   - The Pipeline_FullExtraction test requires Azure AI services configured
#
# Environment:
#   - Requires Azure CLI in PATH (uses /home/dwight/virtualenvs/my_venv/bin)
#   - Requires Docker for SQL Server container
#   - Requires dotnet-ef tool for migrations
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
APPHOST_DIR="$PROJECT_ROOT/src/SessionSight.AppHost"
LOG_FILE="/tmp/aspire-e2e.log"
MAX_WAIT_SECONDS=120
POLL_INTERVAL=2

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() { echo -e "${GREEN}[E2E]${NC} $1"; }
warn() { echo -e "${YELLOW}[E2E]${NC} $1"; }
error() { echo -e "${RED}[E2E]${NC} $1"; }

cleanup() {
    log "Cleaning up..."
    # Kill all related processes - order matters (children before parents)
    pkill -9 -f "SessionSight.Api" 2>/dev/null || true
    pkill -9 -f "SessionSight.AppHost" 2>/dev/null || true
    pkill -9 -f "SessionSight" 2>/dev/null || true
    pkill -9 -f "Aspire.Dashboard" 2>/dev/null || true
    pkill -9 -f "Aspire" 2>/dev/null || true
    pkill -9 -f "dcpctrl" 2>/dev/null || true
    pkill -9 -f "dcp" 2>/dev/null || true
}

trap cleanup EXIT

# Step 1: Kill existing processes
log "Stopping any existing Aspire/SessionSight processes..."
cleanup
sleep 3

# Step 2: Ensure Azure CLI is in PATH
export PATH="/home/dwight/virtualenvs/my_venv/bin:$PATH"
if ! command -v az &> /dev/null; then
    error "Azure CLI not found in PATH. Add your venv to PATH."
    exit 1
fi

# Step 3: Remove old SQL container for fresh database (optional: use --keep-db to preserve)
if [ "$1" != "--keep-db" ]; then
    SQL_CONTAINER=$(docker ps -a --format '{{.Names}}' | grep sql || true)
    if [ -n "$SQL_CONTAINER" ]; then
        log "Removing old SQL container for fresh database..."
        docker rm -f "$SQL_CONTAINER" 2>/dev/null || true
        sleep 2
    fi
fi

# Step 4: Start Aspire
log "Starting Aspire..."
cd "$APPHOST_DIR"
dotnet run > "$LOG_FILE" 2>&1 &
ASPIRE_PID=$!
log "Aspire started with PID $ASPIRE_PID (log: $LOG_FILE)"

# Step 5: Wait for API to be ready by polling /health
log "Waiting for API to be ready..."
SECONDS_WAITED=0
API_PORT=""

while [ $SECONDS_WAITED -lt $MAX_WAIT_SECONDS ]; do
    # Find HTTP port by checking SessionSight.Api process
    HTTP_PORTS=$(ss -tlnp 2>/dev/null | grep "SessionSight.Ap" | grep -oP '127\.0\.0\.1:\K[0-9]+' | sort -u)

    for PORT in $HTTP_PORTS; do
        # Check if this port redirects to HTTPS (API behavior)
        REDIRECT=$(curl -sI "http://localhost:$PORT/health" 2>/dev/null | grep -i "Location:" | grep -oP 'https://localhost:\K[0-9]+' || true)
        if [ -n "$REDIRECT" ]; then
            # Found the redirect - now check if HTTPS is healthy
            if curl -sk "https://localhost:$REDIRECT/health" 2>/dev/null | grep -q "Healthy"; then
                API_PORT=$REDIRECT
                break 2
            fi
        fi
    done

    sleep $POLL_INTERVAL
    SECONDS_WAITED=$((SECONDS_WAITED + POLL_INTERVAL))
    echo -n "."
done
echo ""

if [ -z "$API_PORT" ]; then
    error "API did not become ready within $MAX_WAIT_SECONDS seconds"
    error "Last log entries:"
    tail -20 "$LOG_FILE"
    exit 1
fi

log "API is ready on HTTPS port $API_PORT"

# Step 6: Run migrations and insert test data
log "Running database migrations..."
SQL_PASSWORD=$(dotnet user-secrets list --project "$APPHOST_DIR" 2>/dev/null | grep sql-password | cut -d'=' -f2 | tr -d ' ')
SQL_CONTAINER=$(docker ps --format '{{.Names}}' | grep sql)
SQL_PORT=$(docker port "$SQL_CONTAINER" 1433 2>/dev/null | cut -d: -f2)

dotnet ef database update \
    --project "$PROJECT_ROOT/src/SessionSight.Infrastructure" \
    --startup-project "$PROJECT_ROOT/src/SessionSight.Api" \
    --connection "Server=localhost,$SQL_PORT;Database=sessionsight;User Id=sa;Password=$SQL_PASSWORD;TrustServerCertificate=true" \
    --no-build 2>&1 | tail -5

log "Inserting test therapist..."
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_PASSWORD" -C -d sessionsight \
    -Q "IF NOT EXISTS (SELECT 1 FROM Therapists WHERE Id = '00000000-0000-0000-0000-000000000001')
        INSERT INTO Therapists (Id, Name, LicenseNumber, Credentials, IsActive, CreatedAt)
        VALUES ('00000000-0000-0000-0000-000000000001', 'Test Therapist', 'LIC-001', 'PhD', 1, GETUTCDATE())" 2>/dev/null || true

# Step 7: Run functional tests
log "Running functional tests..."
cd "$PROJECT_ROOT"
export API_BASE_URL="https://localhost:$API_PORT"
dotnet test tests/SessionSight.FunctionalTests --verbosity normal

log "E2E tests completed successfully!"
