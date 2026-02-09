#!/bin/bash
# =============================================================================
# Coverage Check Script
# =============================================================================
# Runs tests with coverage and validates against threshold (82%, 2% above SonarCloud)
#
# Usage:
#   ./scripts/check-coverage.sh           # Run tests and check coverage
#   ./scripts/check-coverage.sh --report  # Also open coverage report
# =============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
# Coverage threshold: 82% (2% above SonarCloud's 80% requirement, 1% above CI)
THRESHOLD=0.82
THRESHOLD_PERCENT=82

cd "$PROJECT_ROOT"

echo "Cleaning previous coverage artifacts..."
rm -rf coverage
mkdir -p coverage

echo "Running tests with coverage..."
dotnet test session-sight.sln \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory ./coverage \
    --filter "Category!=Functional" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo "Generating coverage report..."
# Exclude infrastructure code that requires external services (Azure, EF Core) to test:
# - Migrations (EF Core generated)
# - Azure SDK wrappers (AIFoundryClientFactory, DocumentIntelligenceParser, AzureBlobDocumentStorage, EmbeddingService)
# - OpenAI SDK wrapper (AgentLoopRunner)
# - Azure Functions (ProcessIncomingNoteFunction)
# - EF Core infrastructure (SessionSightDbContext, SessionRepository, PatientRepository, DependencyInjection)
dotnet reportgenerator \
    -reports:"coverage/**/coverage.cobertura.xml" \
    -targetdir:coverage/report \
    -reporttypes:Cobertura,Html \
    -filefilters:"-**/Migrations/**;-**/AIFoundryClientFactory.cs;-**/DocumentIntelligenceParser.cs;-**/AzureBlobDocumentStorage.cs;-**/AgentLoopRunner.cs;-**/DependencyInjection.cs;-**/SessionSightDbContext.cs;-**/SessionRepository.cs;-**/PatientRepository.cs;-**/ReviewRepository.cs;-**/ProcessIncomingNoteFunction.cs;-**/SearchIndexService.cs;-**/SearchIndexInitializer.cs;-**/EmbeddingService.cs;-**/obj/**"

# Check threshold
COVERAGE=$(grep -oP 'line-rate="\K[^"]+' coverage/report/Cobertura.xml | head -1)
PERCENT=$(echo "$COVERAGE * 100" | bc)

echo ""
echo "=========================================="
echo "Coverage: $PERCENT%"
echo "Threshold: $THRESHOLD_PERCENT% (CI: 81%, SonarCloud: 80%)"
echo "=========================================="

if (( $(echo "$COVERAGE < $THRESHOLD" | bc -l) )); then
    echo "FAILED: Coverage $PERCENT% is below $THRESHOLD_PERCENT% threshold"
    exit 1
else
    echo "PASSED: Coverage meets threshold"
fi

# Open report if requested
if [[ "$1" == "--report" ]]; then
    xdg-open coverage/report/index.html 2>/dev/null || open coverage/report/index.html 2>/dev/null || echo "Report: coverage/report/index.html"
fi
