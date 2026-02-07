using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
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
/// Functional tests for Azure AI Search index configuration and embedding pipeline.
/// These tests verify the search index exists with correct schema and that
/// extractions are properly indexed with embeddings.
/// </summary>
[Trait("Category", "Functional")]
public class SearchIndexTests : IClassFixture<ApiFixture>
{
    private readonly string _searchEndpoint;
    private readonly string _indexName;
    private readonly HttpClient _client;
    private readonly HttpClient _longClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public SearchIndexTests(ApiFixture fixture)
    {
        _client = fixture.Client;
        _longClient = fixture.LongClient;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Try multiple environment variable formats (double underscore for Aspire, colon for standard)
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
    public async Task SearchIndex_HasCorrectSchema()
    {
        var credential = new DefaultAzureCredential();
        var indexClient = new SearchIndexClient(new Uri(_searchEndpoint), credential);

        var index = await indexClient.GetIndexAsync(_indexName);

        index.Value.Should().NotBeNull("Index should exist");
        index.Value.Name.Should().Be(_indexName);

        var fields = index.Value.Fields;
        fields.Should().NotBeEmpty("Index should have fields");

        // Verify key field
        var idField = fields.FirstOrDefault(f => f.Name == "Id");
        idField.Should().NotBeNull("Id field should exist");
        idField!.IsKey.Should().BeTrue("Id should be the key field");

        // Verify filterable fields
        var patientIdField = fields.FirstOrDefault(f => f.Name == "PatientId");
        patientIdField.Should().NotBeNull("PatientId field should exist");
        patientIdField!.IsFilterable.Should().BeTrue("PatientId should be filterable");

        var sessionIdField = fields.FirstOrDefault(f => f.Name == "SessionId");
        sessionIdField.Should().NotBeNull("SessionId field should exist");
        sessionIdField!.IsFilterable.Should().BeTrue("SessionId should be filterable");

        // Verify vector field
        var vectorField = fields.FirstOrDefault(f => f.Name == "ContentVector");
        vectorField.Should().NotBeNull("ContentVector field should exist");

        // Verify vector search configuration
        index.Value.VectorSearch.Should().NotBeNull("Vector search should be configured");
        index.Value.VectorSearch!.Profiles.Should().NotBeEmpty("Vector search profiles should be defined");

        var profile = index.Value.VectorSearch.Profiles.FirstOrDefault(p => p.Name == "vector-profile");
        profile.Should().NotBeNull("vector-profile should exist");
    }

    /// <summary>
    /// Verifies that the extraction pipeline generates embeddings and indexes sessions.
    /// This test runs the full pipeline: create patient → create session → upload → extract → verify indexed.
    /// </summary>
    [Fact]
    public async Task Extraction_IndexesSessionWithEmbedding()
    {
        // 1. Create patient
        var patientRequest = new
        {
            externalId = $"EMB-{Guid.NewGuid():N}",
            firstName = "Embedding",
            lastName = "Test",
            dateOfBirth = "1990-01-15"
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

        // 4. Trigger extraction (this generates embedding and indexes) — uses long timeout
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

        // 4b. Verify extracted fields contain actual clinical data
        await ExtractionAssertions.AssertExtractionFields(_client, sessionId);

        // 5. Query search index with retry (indexing is near-real-time, not instant)
        var searchClient = new SearchClient(
            new Uri(_searchEndpoint),
            _indexName,
            new DefaultAzureCredential());

        SearchDocument? indexedDocument = null;
        var maxAttempts = 30; // Wait up to 30 seconds for indexing

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await searchClient.GetDocumentAsync<SearchDocument>(sessionId.ToString());
                indexedDocument = response.Value;

                if (indexedDocument?.ContentVector != null && indexedDocument.ContentVector.Count > 0)
                {
                    break; // Found document with embedding
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Document not indexed yet, keep waiting
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(2000); // Wait 2 seconds before retry
            }
        }

        // 6. Verify the document was indexed with embedding
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
}
