// =============================================================================
// SessionSight Load Test (k6)
// =============================================================================
// Validates API can handle concurrent users without falling over.
//
// Usage:
//   ./scripts/load-test.sh                          # Cheap endpoints only
//   LOAD_TEST_EXPENSIVE=true ./scripts/load-test.sh # Include LLM endpoints
//
// Scenarios:
//   - cheap: GET endpoints (health, patients, sessions, review queue)
//   - expensive: Full pipeline (create patient -> session -> upload -> extract -> Q&A)
// =============================================================================

import http from 'k6/http';
import { check, sleep, group } from 'k6';

// Load sample PDF for document upload (relative to this script's location)
const pdfData = open('../../tests/SessionSight.FunctionalTests/TestData/sample-note.pdf', 'b');

// -----------------------------------------------------------------------------
// Configuration
// -----------------------------------------------------------------------------

const BASE_URL = __ENV.API_URL || 'https://localhost:7039';
const RUN_EXPENSIVE = __ENV.EXPENSIVE === 'true';

// Build scenarios object
const scenarios = {
    // Cheap scenario: fast endpoints, quick validation
    cheap: {
        executor: 'ramping-vus',
        startVUs: 0,
        stages: RUN_EXPENSIVE
            ? [{ duration: '5s', target: 5 }, { duration: '5s', target: 0 }]  // Quick 10s when running expensive
            : [{ duration: '5s', target: 10 }, { duration: '20s', target: 10 }, { duration: '5s', target: 0 }], // Full 30s standalone
        gracefulRampDown: '3s',
        exec: 'cheapScenario',
    },
};

// Add expensive scenario if enabled
if (RUN_EXPENSIVE) {
    scenarios.expensive = {
        executor: 'shared-iterations',
        vus: 2,           // 2 parallel (Azure OpenAI rate limits kick in at 3+)
        iterations: 4,    // 4 total (~$0.10, fits under rate limits)
        maxDuration: '8m',// Allow time for retries on rate limits
        exec: 'expensiveScenario',
        startTime: '12s', // Start right after quick cheap scenario
    };
}

// Build thresholds object
const thresholds = {
    // Cheap endpoints: P95 < 500ms, error rate < 1%
    'http_req_duration{scenario:cheap}': ['p(95)<500'],
    'http_req_failed{scenario:cheap}': ['rate<0.01'],
};

// Add expensive thresholds if enabled
if (RUN_EXPENSIVE) {
    // P95 < 5min allows for Azure OpenAI rate limit retries with exponential backoff
    thresholds['http_req_duration{scenario:expensive}'] = ['p(95)<300000'];
    thresholds['http_req_failed{scenario:expensive}'] = ['rate<0.05'];
}

export const options = {
    insecureSkipTLSVerify: true, // Self-signed certs in dev
    scenarios: scenarios,
    thresholds: thresholds,
};

// -----------------------------------------------------------------------------
// Setup: Create test data
// -----------------------------------------------------------------------------

export function setup() {
    console.log(`Load test starting against ${BASE_URL}`);
    console.log(`Expensive scenarios: ${RUN_EXPENSIVE ? 'ENABLED' : 'disabled'}`);

    // Health check first
    const healthRes = http.get(`${BASE_URL}/health`);
    if (healthRes.status !== 200) {
        throw new Error(`API not healthy: ${healthRes.status} ${healthRes.body}`);
    }

    // Get existing patients (seeded by start-dev.sh) - needed for cheap scenario
    const patientsRes = http.get(`${BASE_URL}/api/patients`);
    let patients = [];
    try {
        patients = JSON.parse(patientsRes.body) || [];
    } catch (e) {
        patients = [];
    }

    if (patients.length === 0) {
        if (RUN_EXPENSIVE) {
            // Expensive scenario creates its own data, so no seeded data is fine
            console.log('No seeded patients found. Expensive scenario will create its own data.');
            return {
                patientId: null,
                sessionId: null,
                patientCount: 0,
                sessionCount: 0,
                hasExtractions: false,
            };
        } else {
            throw new Error('No patients found. Run ./scripts/start-dev.sh first to seed data.');
        }
    }

    const patientId = patients[0].id;
    console.log(`Using patient: ${patientId}`);

    // Get existing sessions
    const sessionsRes = http.get(`${BASE_URL}/api/sessions?patientId=${patientId}`);
    let sessions = [];
    try {
        sessions = JSON.parse(sessionsRes.body) || [];
    } catch (e) {
        sessions = [];
    }

    let sessionId = null;
    let hasExtractions = false;

    if (sessions.length > 0) {
        sessionId = sessions[0].id;
        console.log(`Using session: ${sessionId}`);

        // Check if any session has an extraction (needed for Q&A)
        const extractionRes = http.get(`${BASE_URL}/api/sessions/${sessionId}/extraction`);
        hasExtractions = (extractionRes.status === 200);
        console.log(`Has extractions: ${hasExtractions}`);
    }

    return {
        patientId: patientId,
        sessionId: sessionId,
        patientCount: patients.length,
        sessionCount: sessions.length,
        hasExtractions: hasExtractions,
    };
}

// -----------------------------------------------------------------------------
// Cheap Scenario: Fast GET endpoints
// -----------------------------------------------------------------------------

export function cheapScenario(data) {
    group('health', () => {
        const res = http.get(`${BASE_URL}/health`);
        check(res, {
            'health is 200': (r) => r.status === 200,
            'health is Healthy': (r) => r.body && r.body.includes('Healthy'),
        });
    });

    group('patients', () => {
        // List all patients
        const listRes = http.get(`${BASE_URL}/api/patients`);
        check(listRes, {
            'patients list is 200': (r) => r.status === 200,
            'patients is array': (r) => {
                try {
                    return Array.isArray(JSON.parse(r.body));
                } catch (e) {
                    return false;
                }
            },
        });

        // Get single patient
        if (data.patientId) {
            const getRes = http.get(`${BASE_URL}/api/patients/${data.patientId}`);
            check(getRes, {
                'patient get is 200': (r) => r.status === 200,
            });
        }
    });

    group('sessions', () => {
        // List all sessions
        const listRes = http.get(`${BASE_URL}/api/sessions`);
        check(listRes, {
            'sessions list is 200': (r) => r.status === 200,
        });

        // Get single session
        if (data.sessionId) {
            const getRes = http.get(`${BASE_URL}/api/sessions/${data.sessionId}`);
            check(getRes, {
                'session get is 200': (r) => r.status === 200,
            });
        }
    });

    group('review', () => {
        const res = http.get(`${BASE_URL}/api/review/queue`);
        check(res, {
            'review queue is 200': (r) => r.status === 200,
        });

        const statsRes = http.get(`${BASE_URL}/api/review/stats`);
        check(statsRes, {
            'review stats is 200': (r) => r.status === 200,
        });
    });

    // Brief pause between iterations
    sleep(0.5);
}

// -----------------------------------------------------------------------------
// Expensive Scenario: Full pipeline (create data -> extract -> Q&A)
// -----------------------------------------------------------------------------

export function expensiveScenario(data) {
    const headers = { 'Content-Type': 'application/json' };
    const uniqueId = `load-${Date.now()}-${__VU}-${__ITER}`;

    // 1. Create patient
    let patientId;
    group('create-patient', () => {
        const res = http.post(`${BASE_URL}/api/patients`, JSON.stringify({
            externalId: uniqueId,
            firstName: 'Load',
            lastName: 'Test',
            dateOfBirth: '1990-01-01',
        }), { headers });

        const success = check(res, {
            'patient created (201)': (r) => r.status === 201,
        });

        if (success) {
            patientId = JSON.parse(res.body).id;
        } else {
            console.error(`Patient creation failed: ${res.status} ${res.body}`);
        }
    });

    if (!patientId) {
        console.error('Aborting expensive scenario - patient creation failed');
        return;
    }

    // 2. Create session
    let sessionId;
    group('create-session', () => {
        const res = http.post(`${BASE_URL}/api/sessions`, JSON.stringify({
            patientId: patientId,
            therapistId: '00000000-0000-0000-0000-000000000001',
            sessionDate: new Date().toISOString().split('T')[0],
            sessionType: 'Individual',
            modality: 'InPerson',
            sessionNumber: 1,
        }), { headers });

        const success = check(res, {
            'session created (201)': (r) => r.status === 201,
        });

        if (success) {
            sessionId = JSON.parse(res.body).id;
        } else {
            console.error(`Session creation failed: ${res.status} ${res.body}`);
        }
    });

    if (!sessionId) {
        console.error('Aborting expensive scenario - session creation failed');
        return;
    }

    // 3. Upload document (native k6 multipart form)
    group('upload-document', () => {
        const res = http.post(
            `${BASE_URL}/api/sessions/${sessionId}/document`,
            { file: http.file(pdfData, 'sample-note.pdf', 'application/pdf') }
        );

        const success = check(res, {
            'document uploaded (201)': (r) => r.status === 201,
        });

        if (!success) {
            console.error(`Document upload failed: ${res.status} ${res.body}`);
        }
    });

    // 4. Run extraction (LLM call, can take 2-5min with rate limit retries)
    group('extraction', () => {
        const res = http.post(
            `${BASE_URL}/api/extraction/${sessionId}`,
            null,
            { timeout: '360s' }
        );

        const success = check(res, {
            'extraction is 200': (r) => r.status === 200,
        });

        if (!success) {
            console.error(`Extraction failed: ${res.status} ${res.body}`);
        }
    });

    // 5. Run Q&A (now has data to search)
    group('qa', () => {
        const res = http.post(
            `${BASE_URL}/api/qa/patient/${patientId}`,
            JSON.stringify({ question: 'What symptoms were discussed?' }),
            { headers, timeout: '60s' }
        );

        const success = check(res, {
            'qa is 200': (r) => r.status === 200,
            'qa has answer': (r) => {
                try {
                    const body = JSON.parse(r.body);
                    return body.answer && body.answer.length > 0;
                } catch (e) {
                    return false;
                }
            },
        });

        if (!success) {
            console.error(`Q&A failed: ${res.status} ${res.body}`);
        }
    });
}

// -----------------------------------------------------------------------------
// Teardown
// -----------------------------------------------------------------------------

export function teardown(data) {
    console.log('Load test completed.');
    console.log(`Cheap scenario used patient: ${data.patientId}`);
    if (RUN_EXPENSIVE) {
        console.log('Expensive scenarios created their own test data (patient + session + extraction + Q&A).');
    }
}
