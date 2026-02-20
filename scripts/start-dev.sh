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

export PATH="/home/dwight/virtualenvs/my_venv/bin:$PATH"

# Check Azure CLI login — required for LLM endpoints (Q&A, extraction, summarization)
if ! az account show > /dev/null 2>&1; then
    echo -e "\033[0;33m[WARN]\033[0m Azure CLI not logged in. LLM features (Q&A, extraction, regeneration) will fail."
    echo -e "\033[0;33m[WARN]\033[0m Run: az login"
fi

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
    return 0
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

# Step 5: Seed demo data (conditional — skips if already seeded)
API="https://localhost:$API_PORT"
THERAPIST_ID="00000000-0000-0000-0000-000000000001"
SAMPLES_DIR="$PROJECT_ROOT/src/SessionSight.Web/public/samples"

PATIENT_COUNT=$(curl -sk "$API/api/patients" 2>/dev/null | jq 'length')
if [[ "$PATIENT_COUNT" -ge 3 ]]; then
    log "Demo data already seeded ($PATIENT_COUNT patients found). Skipping."
else
    log "Seeding demo data (8 patients with full extraction)..."

    # Patient definitions: externalId|firstName|lastName|dob|sessionType|modality|pdf|sessionDate
    PATIENTS=(
        "DEMO-001|Sarah|Chen|1991-06-14|Individual|InPerson|sample-nonrisk-001.pdf|2026-03-05"
        "DEMO-002|Marcus|Williams|1988-11-22|Individual|TelehealthVideo|sample-nonrisk-002.pdf|2026-03-10"
        "DEMO-003|Elena|Rodriguez|1995-02-08|Individual|InPerson|sample-nonrisk-003.pdf|2026-03-12"
        "DEMO-004|David|Thompson|1983-09-30|Individual|InPerson|sample-nonrisk-004.pdf|2026-03-14"
        "DEMO-005|Jennifer|Walsh|1979-04-17|Termination|InPerson|sample-nonrisk-005.pdf|2026-03-18"
        "DEMO-006|Rachel|Morrison|1997-01-25|Crisis|InPerson|sample-risk-001.pdf|2026-03-20"
        "DEMO-007|Harold|Jacobson|1948-08-03|Individual|InPerson|sample-risk-010.pdf|2026-03-22"
        "DEMO-008|Brian|Okafor|1990-12-11|Intake|InPerson|sample-risk-030.pdf|2026-03-25"
    )

    SESSION_IDS=()
    PATIENT_NAMES=()

    for i in "${!PATIENTS[@]}"; do
        IFS='|' read -r EXT_ID FIRST LAST DOB STYPE MODALITY PDF SDATE <<< "${PATIENTS[$i]}"
        NUM=$((i + 1))

        # Create patient
        P_RESP=$(curl -sk -X POST "$API/api/patients" \
            -H "Content-Type: application/json" \
            -d "{\"externalId\":\"$EXT_ID\",\"firstName\":\"$FIRST\",\"lastName\":\"$LAST\",\"dateOfBirth\":\"$DOB\"}" 2>/dev/null)
        P_ID=$(echo "$P_RESP" | jq -r '.id // empty')

        if [[ -z "$P_ID" ]]; then
            error "Failed to create patient $FIRST $LAST — skipping"
            continue
        fi

        # Create session
        S_RESP=$(curl -sk -X POST "$API/api/sessions" \
            -H "Content-Type: application/json" \
            -d "{\"patientId\":\"$P_ID\",\"therapistId\":\"$THERAPIST_ID\",\"sessionDate\":\"$SDATE\",\"sessionType\":\"$STYPE\",\"modality\":\"$MODALITY\",\"sessionNumber\":1,\"durationMinutes\":50}" 2>/dev/null)
        S_ID=$(echo "$S_RESP" | jq -r '.id // empty')

        if [[ -z "$S_ID" ]]; then
            error "Failed to create session for $FIRST $LAST — skipping"
            continue
        fi

        # Upload PDF
        curl -sk -X POST "$API/api/sessions/$S_ID/document" \
            -F "file=@$SAMPLES_DIR/$PDF;type=application/pdf" > /dev/null 2>&1

        SESSION_IDS+=("$S_ID")
        PATIENT_NAMES+=("$FIRST $LAST")
        log "  [$NUM/8] Created $FIRST $LAST + uploaded $PDF"
    done

    # Launch all extractions in parallel
    if [[ ${#SESSION_IDS[@]} -gt 0 ]]; then
        log "Launching ${#SESSION_IDS[@]} extractions in parallel (~5-8 min)..."
        EXTRACTION_PIDS=()
        for i in "${!SESSION_IDS[@]}"; do
            SID="${SESSION_IDS[$i]}"
            NAME="${PATIENT_NAMES[$i]}"
            (
                RESULT=$(curl -sk -X POST "$API/api/extraction/$SID" --max-time 300 -w "%{http_code}" -o /dev/null 2>/dev/null)
                if [[ "$RESULT" == "200" ]]; then
                    echo -e "\033[0;32m[DEV]\033[0m   ✓ Extraction complete: $NAME"
                else
                    echo -e "\033[0;31m[DEV]\033[0m   ✗ Extraction failed ($RESULT): $NAME"
                fi
            ) &
            EXTRACTION_PIDS+=($!)
        done

        # Wait for all extractions with progress
        COMPLETED=0
        TOTAL=${#EXTRACTION_PIDS[@]}
        while [[ $COMPLETED -lt $TOTAL ]]; do
            COMPLETED=0
            for PID in "${EXTRACTION_PIDS[@]}"; do
                if ! kill -0 "$PID" 2>/dev/null; then
                    COMPLETED=$((COMPLETED + 1))
                fi
            done
            if [[ $COMPLETED -lt $TOTAL ]]; then
                echo -ne "\r\033[0;32m[DEV]\033[0m   Extractions: $COMPLETED/$TOTAL complete..."
                sleep 5
            fi
        done
        echo -e "\r\033[0;32m[DEV]\033[0m   Extractions: $TOTAL/$TOTAL complete.    "
        wait
    fi

    log "Demo data seeded: ${#SESSION_IDS[@]} patients with full extraction."
fi

# Step 6: Start frontend
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
echo "  Therapist: Dr. Sarah Mitchell (PhD, LPC)"
echo ""
echo "  Demo patients:"
echo "    1. Sarah Chen       — Anxiety / CBT"
echo "    2. Marcus Williams  — Depression / Telehealth"
echo "    3. Elena Rodriguez  — PTSD / EMDR"
echo "    4. David Thompson   — Substance Use / MI"
echo "    5. Jennifer Walsh   — Termination / Discharge"
echo "    6. Rachel Morrison  — Active SI (high risk)"
echo "    7. Harold Jacobson  — Elderly Grief / Passive SI"
echo "    8. Brian Okafor     — Intake Eval / Columbia Scale"
echo ""
echo "  Press Ctrl+C to stop frontend"
echo "=========================================="
echo ""

services__api__https__0=https://localhost:$API_PORT npx vite --host
