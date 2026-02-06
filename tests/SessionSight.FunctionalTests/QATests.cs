using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SessionSight.FunctionalTests.Fixtures;

namespace SessionSight.FunctionalTests;

/// <summary>
/// Functional tests for the RAG-powered Q&amp;A endpoint.
/// Requires a running API with Azure OpenAI and Azure AI Search configured.
/// </summary>
[Trait("Category", "Functional")]
[Collection("Sequential")]
public class QATests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly HttpClient _longClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public QATests(ApiFixture fixture)
    {
        _client = fixture.Client;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Extraction pipeline (Doc Intelligence + 3 LLM agents + embedding + indexing) can take 3+ minutes
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _longClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(fixture.BaseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    [Fact]
    public async Task QA_AnswersQuestionAboutExtractedSession()
    {
        // 1. Create patient
        var patientRequest = new
        {
            externalId = $"QA-{Guid.NewGuid():N}",
            firstName = "QA",
            lastName = "Test",
            dateOfBirth = "1985-06-20"
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

        // 3. Upload PDF document
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

        // 4. Trigger extraction (generates embedding and indexes) — uses long timeout
        var extractionResponse = await _longClient.PostAsync($"/api/extraction/{sessionId}", null);
        extractionResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Extraction endpoint should return 200 OK");

        var extractionJson = await extractionResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var success = extractionJson.GetProperty("success").GetBoolean();

        if (!success)
        {
            var errorMessage = extractionJson.TryGetProperty("errorMessage", out var errProp)
                ? errProp.GetString()
                : "Unknown error";
            throw new Exception($"Extraction failed: {errorMessage}");
        }

        // 5. Call Q&A with retry (search indexing is near-real-time, not instant)
        var qaRequest = new { question = "What was discussed in the therapy session?" };
        JsonElement qaJson = default;
        var maxAttempts = 15;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await Task.Delay(2000);

            var qaResponse = await _client.PostAsJsonAsync($"/api/qa/patient/{patientId}", qaRequest);
            qaResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Q&A endpoint should return 200 OK");

            qaJson = await qaResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            var sources = qaJson.GetProperty("sources");

            // Search found results once sources are populated (not the "no indexed sessions" fallback)
            if (sources.GetArrayLength() > 0)
            {
                break;
            }

            if (attempt == maxAttempts)
            {
                var answer = qaJson.GetProperty("answer").GetString();
                throw new Exception(
                    $"Q&A returned no sources after {maxAttempts} attempts. Answer: {answer}. " +
                    "Search index may not have finished indexing the session.");
            }
        }

        // 6. Verify response structure
        qaJson.GetProperty("question").GetString().Should().Be("What was discussed in the therapy session?");
        qaJson.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace("Answer should contain content");
        qaJson.GetProperty("confidence").GetDouble().Should().BeGreaterOrEqualTo(0, "Confidence should be non-negative");
        qaJson.GetProperty("modelUsed").GetString().Should().NotBeNullOrWhiteSpace("ModelUsed should be set");

        // Verify answer is not the error fallback
        qaJson.GetProperty("answer").GetString().Should().NotContain("error occurred",
            "Answer should not be the error fallback");

        // 7. Verify sources reference the correct session
        var finalSources = qaJson.GetProperty("sources");
        finalSources.GetArrayLength().Should().BeGreaterOrEqualTo(1, "Should have at least one source citation");

        var firstSource = finalSources[0];
        firstSource.GetProperty("sessionId").GetString().Should().Be(sessionId.ToString(),
            "Source should reference the extracted session");
        firstSource.GetProperty("relevanceScore").GetDouble().Should().BeGreaterThan(0,
            "Relevance score should be positive");
    }
}
