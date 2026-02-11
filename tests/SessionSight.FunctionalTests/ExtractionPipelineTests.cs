using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Identity;
using Azure.Search.Documents;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SessionSight.FunctionalTests.Fixtures;

namespace SessionSight.FunctionalTests;

/// <summary>
/// DTO for deserializing search documents. Mirrors SearchDocument from Infrastructure.
/// Defined here to keep FunctionalTests independent of main projects.
/// </summary>
public class SearchDocument
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public IReadOnlyList<float>? ContentVector { get; set; }
}

/// <summary>
/// End-to-end functional tests for the extraction pipeline.
/// These tests require a running API instance (via Aspire or direct).
/// </summary>
[Trait("Category", "Functional")]
public class ExtractionPipelineTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly HttpClient _longClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _searchEndpoint;
    private readonly string _indexName;

    public ExtractionPipelineTests(ApiFixture fixture)
    {
        _client = fixture.Client;
        _longClient = fixture.LongClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _searchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("AzureSearch__Endpoint")
            ?? configuration["AzureSearch:Endpoint"]
            ?? throw new InvalidOperationException(
                "Search endpoint not configured. Set AZURE_SEARCH_ENDPOINT or AzureSearch__Endpoint environment variable.");

        _indexName = Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX_NAME")
            ?? configuration["AzureSearch:IndexName"]
            ?? "sessionsight-sessions";
    }

    [Fact]
    public async Task Pipeline_CreatesPatient_Session_UploadsDocument()
    {
        // 1. Create patient
        var patientRequest = new
        {
            externalId = $"TEST-{Guid.NewGuid():N}",
            firstName = "Functional",
            lastName = "Test",
            dateOfBirth = "1990-05-15"
        };

        var patientResponse = await _client.PostAsJsonAsync("/api/patients", patientRequest);
        patientResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "Patient creation should succeed");

        var patientJson = await patientResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var patientId = patientJson.GetProperty("id").GetGuid();
        patientId.Should().NotBeEmpty("Patient should have an ID");

        // 2. Create session
        var sessionRequest = new
        {
            patientId,
            therapistId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            sessionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            sessionType = "Individual",
            modality = "InPerson",
            sessionNumber = 1
        };

        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", sessionRequest);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "Session creation should succeed");

        var sessionJson = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var sessionId = sessionJson.GetProperty("id").GetGuid();
        sessionId.Should().NotBeEmpty("Session should have an ID");

        // 3. Upload document
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-note.txt");
        if (!File.Exists(testDataPath))
        {
            throw new FileNotFoundException($"Test data file not found: {testDataPath}");
        }

        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(testDataPath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "sample-note.txt");

        var uploadResponse = await _client.PostAsync($"/api/sessions/{sessionId}/document", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "Document upload should succeed");

        // Verify upload response
        var uploadJson = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        uploadJson.TryGetProperty("documentId", out _).Should().BeTrue("Upload should return documentId");
        uploadJson.TryGetProperty("blobUri", out _).Should().BeTrue("Upload should return blobUri");
    }

    /// <summary>
    /// Full end-to-end test: Create patient -> Create session -> Upload PDF -> Trigger extraction
    /// -> Verify fields -> Verify Q&A -> Verify search indexing.
    /// Sections 3-4 were merged from QATests and SearchIndexTests to share the single
    /// extraction call (~$0.06 and ~4 min saved per run).
    /// </summary>
    [Fact]
    public async Task Pipeline_FullExtraction_ReturnsSuccess()
    {
        // ── Section 1: Extraction Pipeline ──────────────────────────────
        // Tests that the full extraction pipeline (Doc Intelligence → Intake →
        // ClinicalExtractor → RiskAssessor → Summarizer → Embedding → Index)
        // completes successfully and returns correct metadata.

        // 1. Create patient
        var patientRequest = new
        {
            externalId = $"E2E-{Guid.NewGuid():N}",
            firstName = "EndToEnd",
            lastName = "ExtractionTest",
            dateOfBirth = "1985-06-15"
        };

        var patientResponse = await _client.PostAsJsonAsync("/api/patients", patientRequest);
        patientResponse.StatusCode.Should().Be(HttpStatusCode.Created, "Patient creation should succeed");

        var patientJson = await patientResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var patientId = patientJson.GetProperty("id").GetGuid();

        // 2. Create session
        var sessionRequest = new
        {
            patientId,
            therapistId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            sessionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            sessionType = "Individual",
            modality = "InPerson",
            sessionNumber = 1
        };

        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", sessionRequest);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created, "Session creation should succeed");

        var sessionJson = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var sessionId = sessionJson.GetProperty("id").GetGuid();

        // 3. Upload PDF document (required for Document Intelligence)
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "sample-note.pdf");
        if (!File.Exists(testDataPath))
        {
            throw new FileNotFoundException(
                $"Test PDF not found: {testDataPath}. Ensure sample-note.pdf is in TestData with CopyToOutputDirectory.");
        }

        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(testDataPath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "sample-note.pdf");

        var uploadResponse = await _client.PostAsync($"/api/sessions/{sessionId}/document", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created, "Document upload should succeed");

        // 4. Trigger extraction — uses long timeout (Doc Intelligence + 3 LLM agents + embedding + indexing)
        var extractionResponse = await _longClient.PostAsync($"/api/extraction/{sessionId}", null);
        extractionResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Extraction endpoint should return 200 OK");

        var extractionJson = await extractionResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        // 5. Verify extraction result
        var success = extractionJson.GetProperty("success").GetBoolean();
        var returnedSessionId = extractionJson.GetProperty("sessionId").GetGuid();

        returnedSessionId.Should().Be(sessionId, "Extraction should return the correct session ID");

        if (!success)
        {
            var errorMessage = extractionJson.TryGetProperty("errorMessage", out var errProp)
                ? errProp.GetString()
                : "Unknown error";

            // Provide helpful diagnostics for common failures
            if (errorMessage?.Contains("No backend service") == true)
            {
                throw new Exception(
                    $"AI Foundry → OpenAI connection not configured. Deploy Bicep and verify aiProjectConnection exists. Error: {errorMessage}");
            }

            if (errorMessage?.Contains("credential") == true || errorMessage?.Contains("401") == true)
            {
                throw new Exception(
                    $"Authentication failed. Ensure 'az login' is done and user has Cognitive Services User role. Error: {errorMessage}");
            }

            throw new Exception($"Extraction failed: {errorMessage}");
        }

        success.Should().BeTrue("Extraction should complete successfully");
        extractionJson.GetProperty("extractionId").GetGuid().Should().NotBeEmpty(
            "Successful extraction should return an extraction ID");

        // Verify agent loop metadata is present
        if (extractionJson.TryGetProperty("toolCallCount", out var toolCallProp))
        {
            var toolCallCount = toolCallProp.GetInt32();
            toolCallCount.Should().BeGreaterOrEqualTo(0,
                "Agent loop should track tool call count");
        }

        // Verify modelsUsed is populated
        if (extractionJson.TryGetProperty("modelsUsed", out var modelsProp))
        {
            var models = modelsProp.EnumerateArray().Select(e => e.GetString()).ToList();
            models.Should().NotBeEmpty("At least one model should be used for extraction");
        }

        // ── Section 2: Extraction Field Accuracy (74 fields) ────────────
        // Verifies the extracted clinical fields match expected values from
        // sample-note.pdf. Shared assertion helper in ExtractionAssertions.cs.

        await ExtractionAssertions.AssertExtractionFields(_client, sessionId);

        // ── Section 3: Q&A over Extracted Session ───────────────────────
        // Originally: QATests.QA_AnswersQuestionAboutExtractedSession
        // Merged here to share the extraction (~$0.03 saved per run).
        // Tests that the RAG Q&A pipeline can answer questions using the
        // extracted session data via vector search + LLM generation.
        // Retry loop: search index is near-real-time, not instant.

        var qaRequest = new { question = "What was discussed in the therapy session?" };
        JsonElement qaJson = default;
        var qaMaxAttempts = 15;

        for (int attempt = 1; attempt <= qaMaxAttempts; attempt++)
        {
            await Task.Delay(2000);

            var qaResponse = await _client.PostAsJsonAsync($"/api/qa/patient/{patientId}", qaRequest);
            qaResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Q&A endpoint should return 200 OK");

            qaJson = await qaResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            var sources = qaJson.GetProperty("sources");

            if (sources.GetArrayLength() > 0)
            {
                break;
            }

            if (attempt == qaMaxAttempts)
            {
                var answer = qaJson.GetProperty("answer").GetString();
                throw new Exception(
                    $"Q&A returned no sources after {qaMaxAttempts} attempts. Answer: {answer}. " +
                    "Search index may not have finished indexing the session.");
            }
        }

        qaJson.GetProperty("question").GetString().Should().Be("What was discussed in the therapy session?");
        qaJson.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace("Answer should contain content");
        qaJson.GetProperty("confidence").GetDouble().Should().BeGreaterOrEqualTo(0, "Confidence should be non-negative");
        qaJson.GetProperty("modelUsed").GetString().Should().NotBeNullOrWhiteSpace("ModelUsed should be set");

        qaJson.GetProperty("answer").GetString().Should().NotContain("error occurred",
            "Answer should not be the error fallback");

        var finalSources = qaJson.GetProperty("sources");
        finalSources.GetArrayLength().Should().BeGreaterOrEqualTo(1, "Should have at least one source citation");

        var firstSource = finalSources[0];
        firstSource.GetProperty("sessionId").GetString().Should().Be(sessionId.ToString(),
            "Source should reference the extracted session");
        firstSource.GetProperty("relevanceScore").GetDouble().Should().BeGreaterThan(0,
            "Relevance score should be positive");

        // ── Section 4: Search Index Verification ────────────────────────
        // Originally: SearchIndexTests.Extraction_IndexesSessionWithEmbedding
        // Merged here to share the extraction (~$0.03 saved per run).
        // Tests that the embedding pipeline generated a 3072-dimension vector
        // (text-embedding-3-large) and indexed the session in Azure AI Search.
        // Retry loop: indexing is near-real-time, not instant.

        var searchClient = new SearchClient(
            new Uri(_searchEndpoint),
            _indexName,
            new DefaultAzureCredential());

        SearchDocument? indexedDocument = null;
        var searchMaxAttempts = 30;

        for (int attempt = 1; attempt <= searchMaxAttempts; attempt++)
        {
            try
            {
                var response = await searchClient.GetDocumentAsync<SearchDocument>(sessionId.ToString());
                indexedDocument = response.Value;

                if (indexedDocument?.ContentVector != null && indexedDocument.ContentVector.Count > 0)
                {
                    break;
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Document not indexed yet, keep waiting
            }

            if (attempt < searchMaxAttempts)
            {
                await Task.Delay(2000);
            }
        }

        indexedDocument.Should().NotBeNull(
            $"Session {sessionId} should be indexed in search within 30 seconds. " +
            "Check that AzureSearch:Endpoint is configured and the search service is accessible.");

        indexedDocument!.SessionId.Should().Be(sessionId.ToString(), "SessionId should match");
        indexedDocument.PatientId.Should().Be(patientId.ToString(), "PatientId should match");

        indexedDocument.ContentVector.Should().NotBeNull(
            "ContentVector should be populated. Check that embedding generation is working.");
        indexedDocument.ContentVector!.Count.Should().Be(3072,
            "ContentVector should have 3072 dimensions (text-embedding-3-large)");
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Health check should return OK");
    }

    [Fact]
    public async Task CreatePatient_WithValidData_ReturnsCreated()
    {
        var request = new
        {
            externalId = $"TEST-{Guid.NewGuid():N}",
            firstName = "Jane",
            lastName = "Doe",
            dateOfBirth = "1985-03-20"
        };

        var response = await _client.PostAsJsonAsync("/api/patients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        result.GetProperty("firstName").GetString().Should().Be("Jane");
        result.GetProperty("lastName").GetString().Should().Be("Doe");
    }

    [Fact]
    public async Task CreatePatient_WithInvalidData_ReturnsBadRequest()
    {
        var request = new
        {
            externalId = "", // Invalid: empty
            firstName = "Test",
            lastName = "Test",
            dateOfBirth = "1990-01-01"
        };

        var response = await _client.PostAsJsonAsync("/api/patients", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Empty firstName should be rejected");
    }
}
