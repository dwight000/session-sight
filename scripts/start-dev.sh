#!/bin/bash
# =============================================================================
# SessionSight Dev Starter (Full Stack with Sample Data)
# =============================================================================
# Starts backend, runs migrations, seeds sample data, and starts frontend.
# One command to get everything running for manual testing.
#
# Usage:
#   ./scripts/start-dev.sh
#
# After starting:
#   - Frontend: http://localhost:5173
#   - API: https://localhost:7039
#   - Dashboard: https://localhost:17055
#
# To stop: Ctrl+C (stops frontend), then pkill -f "SessionSight|Aspire|dcp"
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
API_PORT=7039
LOG_ROOT="/tmp/sessionsight"
ASPIRE_LOG="$LOG_ROOT/aspire/aspire-e2e.log"
API_LOG_DIR="$LOG_ROOT/api"

# shellcheck disable=SC2317 # Functions are called dynamically
log() { local msg="$1"; echo -e "\033[0;32m[DEV]\033[0m $msg"; return 0; }
error() { local msg="$1"; echo -e "\033[0;31m[DEV]\033[0m $msg" >&2; return 0; }
print_log_hints() {
    echo "  Troubleshooting logs:"
    echo "    Aspire: $ASPIRE_LOG"
    echo "    API:    $API_LOG_DIR/"
    echo "  First triage commands:"
    echo "    tail -n 200 $ASPIRE_LOG"
    echo "    ls -lah $LOG_ROOT/"
    echo "    ls -lah $API_LOG_DIR/"
    echo "    tail -n 200 \$(ls -1t $API_LOG_DIR/api-*.log 2>/dev/null | head -1)"
}

cd "$PROJECT_ROOT"
mkdir -p "$LOG_ROOT/aspire" "$LOG_ROOT/vite" "$API_LOG_DIR"

# Step 1: Stop existing processes
log "Stopping existing processes..."
pkill -9 -f "SessionSight" 2>/dev/null || true
pkill -9 -f "Aspire" 2>/dev/null || true
pkill -9 -f "dcp" 2>/dev/null || true
pkill -f "node.*vite" 2>/dev/null || true
sleep 2

# Step 2: Start Aspire
log "Starting Aspire..."
nohup dotnet run --project src/SessionSight.AppHost > "$ASPIRE_LOG" 2>&1 &
ASPIRE_PID=$!

# Step 3: Wait for API
log "Waiting for API to be ready..."
SECONDS_WAITED=0
MAX_WAIT=120
while [[ $SECONDS_WAITED -lt $MAX_WAIT ]]; do
    if curl -sk "https://localhost:$API_PORT/health" 2>/dev/null | grep -q "Healthy"; then
        break
    fi
    sleep 1
    SECONDS_WAITED=$((SECONDS_WAITED + 1))
    echo -n "."
done
echo ""

if ! curl -sk "https://localhost:$API_PORT/health" 2>/dev/null | grep -q "Healthy"; then
    error "API did not start within $MAX_WAIT seconds."
    print_log_hints
    exit 1
fi
log "API is ready on https://localhost:$API_PORT"
print_log_hints

# Step 4: Run migrations
log "Running database migrations..."
SQL_PASSWORD=$(dotnet user-secrets list --project src/SessionSight.AppHost 2>/dev/null | grep sql-password | cut -d'=' -f2 | tr -d ' ')
SQL_CONTAINER=$(docker ps --format '{{.Names}}' | grep sql | head -1)
SQL_PORT=$(docker port "$SQL_CONTAINER" 1433 2>/dev/null | cut -d: -f2)

dotnet ef database update \
    --project src/SessionSight.Infrastructure \
    --startup-project src/SessionSight.Api \
    --connection "Server=localhost,$SQL_PORT;Database=sessionsight;User Id=sa;Password=$SQL_PASSWORD;TrustServerCertificate=true" \
    --no-build 2>&1 | tail -3

# Step 5: Insert test therapist
log "Inserting test therapist..."
docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_PASSWORD" -C -d sessionsight \
    -Q "IF NOT EXISTS (SELECT 1 FROM Therapists WHERE Id = '00000000-0000-0000-0000-000000000001')
        INSERT INTO Therapists (Id, Name, LicenseNumber, Credentials, IsActive, CreatedAt)
        VALUES ('00000000-0000-0000-0000-000000000001', 'Test Therapist', 'LIC-001', 'PhD', 1, GETUTCDATE())" 2>/dev/null || true

# Step 6: Seed sample data
log "Seeding sample data..."
API="https://localhost:$API_PORT"

# Create sample patients
P1=$(curl -s -X POST "$API/api/patients" \
    -H "Content-Type: application/json" \
    -d '{"externalId":"P001","firstName":"Sarah","lastName":"Johnson","dateOfBirth":"1985-03-22"}' \
    --insecure 2>/dev/null) # NOSONAR - intentional for localhost self-signed certs
P1_ID=$(echo "$P1" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

P2=$(curl -s -X POST "$API/api/patients" \
    -H "Content-Type: application/json" \
    -d '{"externalId":"P002","firstName":"Michael","lastName":"Chen","dateOfBirth":"1992-07-15"}' \
    --insecure 2>/dev/null) # NOSONAR - intentional for localhost self-signed certs
P2_ID=$(echo "$P2" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

# Create sample sessions
if [[ -n "$P1_ID" ]]; then
    curl -s -X POST "$API/api/sessions" \
        -H "Content-Type: application/json" \
        -d "{\"patientId\":\"$P1_ID\",\"therapistId\":\"00000000-0000-0000-0000-000000000001\",\"sessionDate\":\"2026-02-08\",\"sessionType\":\"Individual\",\"modality\":\"InPerson\",\"sessionNumber\":1,\"durationMinutes\":50}" \
        --insecure > /dev/null 2>&1
fi

log "Sample data created: 2 patients, 1 session"

# Step 7: Start frontend
log "Starting frontend..."
cd src/SessionSight.Web
echo ""
echo "=========================================="
echo "  SessionSight is ready!"
echo "=========================================="
echo ""
echo "  Frontend: http://localhost:5173"
echo "  API:      https://localhost:7039"
echo "  Dashboard: https://localhost:17055"
echo ""
print_log_hints
echo ""
echo "  Sample data:"
echo "    - 2 patients (Sarah Johnson, Michael Chen)"
echo "    - 1 session"
echo ""
echo "  To test extraction:"
echo "    1. Go to Upload page"
echo "    2. Upload: tests/SessionSight.FunctionalTests/TestData/sample-note.pdf"
echo ""
echo "  Press Ctrl+C to stop frontend"
echo "=========================================="
echo ""

services__api__https__0=https://localhost:$API_PORT npx vite --host
