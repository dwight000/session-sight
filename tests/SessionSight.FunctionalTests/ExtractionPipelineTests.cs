using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SessionSight.FunctionalTests.Fixtures;

namespace SessionSight.FunctionalTests;

/// <summary>
/// End-to-end functional tests for the extraction pipeline.
/// These tests require a running API instance (via Aspire or direct).
/// </summary>
[Trait("Category", "Functional")]
public class ExtractionPipelineTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExtractionPipelineTests(ApiFixture fixture)
    {
        _client = fixture.Client;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
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
    /// Full end-to-end test: Create patient → Create session → Upload PDF → Trigger extraction.
    /// This test verifies the AI Foundry → OpenAI connection is working.
    /// Requires: Azure CLI login, Bicep role assignments deployed, AI Project connection configured.
    /// </summary>
    [Fact]
    public async Task Pipeline_FullExtraction_ReturnsSuccess()
    {
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

        // 4. Trigger extraction
        var extractionResponse = await _client.PostAsync($"/api/extraction/{sessionId}", null);
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
