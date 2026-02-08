#!/bin/bash
# =============================================================================
# SessionSight E2E Test Runner
# =============================================================================
# Runs E2E tests against a fresh Aspire instance with clean database.
#
# Usage:
#   ./scripts/run-e2e.sh                      # Backend C# functional tests (default)
#   ./scripts/run-e2e.sh --frontend           # Full-stack Playwright tests (browser + backend)
#   ./scripts/run-e2e.sh --frontend --headed  # Playwright with visible browser
#   ./scripts/run-e2e.sh --all                # Run both backend and frontend tests
#   ./scripts/run-e2e.sh --hot                # Reuse running Aspire (fast iteration)
#   ./scripts/run-e2e.sh --keep-db            # Keep existing database
#   ./scripts/run-e2e.sh --filter "TestName"  # Run specific test(s)
#
# What it does:
#   1. Kills existing SessionSight/Aspire/dcp processes (unless --hot)
#   2. Removes old SQL container (unless --keep-db or --hot)
#   3. Starts Aspire (builds solution if needed)
#   4. Polls /health endpoint until API is ready
#   5. Discovers HTTPS port via HTTP->HTTPS redirect
#   6. Runs EF migrations and inserts test therapist
#   7. Runs tests:
#      - Default: C# functional tests (dotnet test)
#      - --frontend: Starts Vite + runs Playwright fullStack project
#      - --all: Runs both
#
# Cost note (--frontend):
#   Each frontend test run costs ~$0.05-0.10 in LLM tokens (extraction uses GPT-4o).
#   Run sparingly - use mocked smoke tests for rapid iteration.
#
# Troubleshooting:
#   - If tests fail, check /tmp/aspire-e2e.log for Aspire output
#   - API is on fixed port: https://localhost:7039
#   - If migrations fail, ensure SQL container is running: docker ps | grep sql
#   - Aspire dashboard: https://localhost:17055 (shows traces)
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
WEB_DIR="$PROJECT_ROOT/src/SessionSight.Web"
LOG_FILE="/tmp/aspire-e2e.log"
VITE_LOG="/tmp/vite-e2e.log"
MAX_WAIT_SECONDS=120
POLL_INTERVAL=2
VITE_PORT=5173

# Parse arguments
HOT_MODE=false
KEEP_DB=false
RUN_BACKEND=false
RUN_FRONTEND=false
HEADED=""
TEST_FILTER=""

for arg in "$@"; do
    case $arg in
        --hot)
            HOT_MODE=true
            KEEP_DB=true
            ;;
        --keep-db)
            KEEP_DB=true
            ;;
        --frontend)
            RUN_FRONTEND=true
            ;;
        --all)
            RUN_BACKEND=true
            RUN_FRONTEND=true
            ;;
        --headed)
            HEADED="--headed"
            ;;
        --filter)
            shift
            TEST_FILTER="$1"
            ;;
        --filter=*)
            TEST_FILTER="${arg#*=}"
            ;;
        *)
            # Ignore unknown arguments
            ;;
    esac
done

# Default to backend tests if neither --frontend nor --all specified
if [[ "$RUN_FRONTEND" = false && "$RUN_BACKEND" = false ]]; then
    RUN_BACKEND=true
fi

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() { local msg="$1"; echo -e "${GREEN}[E2E]${NC} $msg"; return 0; }
warn() { local msg="$1"; echo -e "${YELLOW}[E2E]${NC} $msg"; return 0; }
error() { local msg="$1"; echo -e "${RED}[E2E]${NC} $msg"; return 0; }

# Track PIDs for cleanup
VITE_PID=""

cleanup_vite() {
    if [[ -n "$VITE_PID" ]]; then
        log "Stopping Vite server (PID $VITE_PID)..."
        kill "$VITE_PID" 2>/dev/null || true
    fi
    pkill -f "vite.*SessionSight.Web" 2>/dev/null || true
    return 0
}

cleanup() {
    cleanup_vite
    if [[ "$HOT_MODE" = true ]]; then
        log "Hot mode: keeping Aspire running for next iteration"
        return
    fi
    log "Cleaning up..."
    pkill -9 -f "SessionSight.Api" 2>/dev/null || true
    pkill -9 -f "SessionSight.AppHost" 2>/dev/null || true
    pkill -9 -f "SessionSight" 2>/dev/null || true
    pkill -9 -f "Aspire.Dashboard" 2>/dev/null || true
    pkill -9 -f "Aspire" 2>/dev/null || true
    pkill -9 -f "dcpctrl" 2>/dev/null || true
    pkill -9 -f "dcp" 2>/dev/null || true
    docker ps -a --format '{{.Names}}' | grep -E 'sql-|storage-' | xargs -r docker rm -f 2>/dev/null || true
    docker network ls --filter "name=aspire" -q | xargs -r docker network rm 2>/dev/null || true
}

trap cleanup EXIT

# Step 1: Kill existing processes (skip in hot mode)
if [[ "$HOT_MODE" = true ]]; then
    log "Hot mode: checking if Aspire is already running..."
    if pgrep -f "SessionSight" > /dev/null; then
        log "Aspire is running - reusing existing instance"
    else
        warn "Aspire not running - starting fresh (use regular mode next time)"
        HOT_MODE=false
    fi
else
    log "Stopping any existing Aspire/SessionSight processes..."
    pkill -9 -f "SessionSight.Api" 2>/dev/null || true
    pkill -9 -f "SessionSight.AppHost" 2>/dev/null || true
    pkill -9 -f "SessionSight" 2>/dev/null || true
    pkill -9 -f "Aspire.Dashboard" 2>/dev/null || true
    pkill -9 -f "Aspire" 2>/dev/null || true
    pkill -9 -f "dcpctrl" 2>/dev/null || true
    pkill -9 -f "dcp" 2>/dev/null || true
    sleep 3
fi

# Step 2: Ensure Azure CLI is in PATH
export PATH="/home/dwight/virtualenvs/my_venv/bin:$PATH"
if ! command -v az &> /dev/null; then
    error "Azure CLI not found in PATH. Add your venv to PATH."
    exit 1
fi

# Step 3: Remove old SQL container for fresh database (skip if --keep-db or --hot)
if [[ "$KEEP_DB" = false ]]; then
    SQL_CONTAINER=$(docker ps -a --format '{{.Names}}' | grep sql || true)
    if [[ -n "$SQL_CONTAINER" ]]; then
        log "Removing old SQL container for fresh database..."
        docker rm -f "$SQL_CONTAINER" 2>/dev/null || true
        sleep 2
    fi
fi

# Step 4: Start Aspire (skip if hot mode and already running)
if [[ "$HOT_MODE" = true ]] && pgrep -f "SessionSight" > /dev/null; then
    log "Reusing existing Aspire instance..."
else
    log "Starting Aspire..."
    cd "$APPHOST_DIR"
    dotnet run > "$LOG_FILE" 2>&1 &
    ASPIRE_PID=$!
    log "Aspire started with PID $ASPIRE_PID (log: $LOG_FILE)"
fi

# Step 5: Wait for API to be ready by polling /health (fixed port 7039)
log "Waiting for API to be ready..."
SECONDS_WAITED=0
API_PORT=7039

while [[ $SECONDS_WAITED -lt $MAX_WAIT_SECONDS ]]; do
    if curl -sk "https://localhost:$API_PORT/health" 2>/dev/null | grep -q "Healthy"; then
        break
    fi
    sleep $POLL_INTERVAL
    SECONDS_WAITED=$((SECONDS_WAITED + POLL_INTERVAL))
    echo -n "."
done
echo ""

if ! curl -sk "https://localhost:$API_PORT/health" 2>/dev/null | grep -q "Healthy"; then
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

# Step 7: Run backend tests (C# functional tests)
if [[ "$RUN_BACKEND" = true ]]; then
    log "Running backend functional tests..."
    cd "$PROJECT_ROOT"
    export API_BASE_URL="https://localhost:$API_PORT"

    SEARCH_ENDPOINT=$(dotnet user-secrets list --project "$APPHOST_DIR" 2>/dev/null | grep "search-endpoint" | cut -d'=' -f2 | tr -d ' ')
    if [[ -n "$SEARCH_ENDPOINT" ]]; then
        export AzureSearch__Endpoint="$SEARCH_ENDPOINT"
        log "Using search endpoint from user secrets"
    fi

    if [[ -n "$TEST_FILTER" ]]; then
        log "Filter: $TEST_FILTER"
        dotnet test tests/SessionSight.FunctionalTests --verbosity normal --filter "FullyQualifiedName~$TEST_FILTER"
    else
        dotnet test tests/SessionSight.FunctionalTests --verbosity normal
    fi
    log "Backend tests completed!"
fi

# Step 8: Run frontend tests (Playwright full-stack)
if [[ "$RUN_FRONTEND" = true ]]; then
    log "Running frontend full-stack tests..."

    # Kill any existing Vite process on port 5173
    if lsof -ti:$VITE_PORT > /dev/null 2>&1; then
        log "Stopping existing process on port $VITE_PORT..."
        lsof -ti:$VITE_PORT | xargs kill 2>/dev/null || true
        sleep 1
    fi

    # Start Vite with API URL configured
    log "Starting Vite dev server..."
    cd "$WEB_DIR"
    export services__api__https__0="https://localhost:$API_PORT"
    npm run dev > "$VITE_LOG" 2>&1 &
    VITE_PID=$!
    log "Vite started with PID $VITE_PID (log: $VITE_LOG)"

    # Wait for Vite to be ready
    log "Waiting for Vite to be ready..."
    SECONDS_WAITED=0
    VITE_MAX_WAIT=30

    while [[ $SECONDS_WAITED -lt $VITE_MAX_WAIT ]]; do
        if curl -s "http://localhost:$VITE_PORT" > /dev/null 2>&1; then
            break
        fi
        sleep 1
        SECONDS_WAITED=$((SECONDS_WAITED + 1))
        echo -n "."
    done
    echo ""

    if ! curl -s "http://localhost:$VITE_PORT" > /dev/null 2>&1; then
        error "Vite did not start within $VITE_MAX_WAIT seconds"
        tail -20 "$VITE_LOG"
        exit 1
    fi

    log "Vite is ready on port $VITE_PORT"

    # Run Playwright tests
    log "Running Playwright full-stack tests..."
    PLAYWRIGHT_ARGS="--project=fullStack $HEADED"
    if [[ -n "$TEST_FILTER" ]]; then
        PLAYWRIGHT_ARGS="$PLAYWRIGHT_ARGS --grep \"$TEST_FILTER\""
    fi

    npx playwright test $PLAYWRIGHT_ARGS

    # Stop Vite
    cleanup_vite
    log "Frontend tests completed!"
fi

log "E2E tests completed successfully!"
log "Aspire dashboard: https://localhost:17055 (view traces)"
