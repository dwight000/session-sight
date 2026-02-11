using Azure.Identity;
using Azure.Search.Documents.Indexes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace SessionSight.FunctionalTests;

/// <summary>
/// Functional tests for Azure AI Search index configuration.
/// Verifies the search index exists with correct schema.
/// The embedding/indexing E2E test was merged into ExtractionPipelineTests
/// to share the single extraction call (~$0.03 saved per run).
/// </summary>
[Trait("Category", "Functional")]
public class SearchIndexTests
{
    private readonly string _searchEndpoint;
    private readonly string _indexName;

    public SearchIndexTests()
    {
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
}
