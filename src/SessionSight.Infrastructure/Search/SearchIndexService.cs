using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionSight.Core.Resilience;
using AzureSearchClientOptions = Azure.Search.Documents.SearchClientOptions;
using AzureSearchOptions = Azure.Search.Documents.SearchOptions;

namespace SessionSight.Infrastructure.Search;

/// <summary>
/// Azure AI Search implementation of the search index service.
/// Gracefully degrades when not configured - operations become no-ops.
/// </summary>
public partial class SearchIndexService : ISearchIndexService
{
    private readonly SearchIndexClient? _indexClient;
    private readonly SearchClient? _searchClient;
    private readonly SearchOptions _options;
    private readonly ILogger<SearchIndexService> _logger;
    private readonly bool _isConfigured;

    public SearchIndexService(
        IOptions<SearchOptions> options,
        ILogger<SearchIndexService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Log configuration values at startup for diagnostics
        LogConfigurationValues(_logger, _options.Endpoint ?? "(null)", _options.IndexName);

        if (string.IsNullOrEmpty(_options.Endpoint))
        {
            LogNotConfigured(_logger);
            _isConfigured = false;
            return;
        }

        _isConfigured = true;
        var endpoint = new Uri(_options.Endpoint);
        var credential = new DefaultAzureCredential();
        var clientOptions = AzureRetryDefaults.Configure(new AzureSearchClientOptions());

        _indexClient = new SearchIndexClient(endpoint, credential, clientOptions);
        _searchClient = new SearchClient(endpoint, _options.IndexName, credential, clientOptions);
    }

    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConfigured || _indexClient is null)
        {
            return;
        }

        var index = BuildIndexDefinition();

        await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
        LogIndexReady(_logger, _options.IndexName);
    }

    public async Task IndexDocumentAsync(SessionSearchDocument document, CancellationToken cancellationToken = default)
    {
        if (!_isConfigured || _searchClient is null)
        {
            LogIndexingSkipped(_logger, document.Id);
            return;
        }

        await _searchClient.MergeOrUploadDocumentsAsync(
            new[] { document },
            cancellationToken: cancellationToken);

        LogDocumentIndexed(_logger, document.Id);
    }

    public async Task IndexDocumentsAsync(IEnumerable<SessionSearchDocument> documents, CancellationToken cancellationToken = default)
    {
        if (!_isConfigured || _searchClient is null)
        {
            return;
        }

        var documentList = documents.ToList();
        if (documentList.Count == 0)
        {
            return;
        }

        await _searchClient.MergeOrUploadDocumentsAsync(
            documentList,
            cancellationToken: cancellationToken);

        LogDocumentsIndexed(_logger, documentList.Count);
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (!_isConfigured || _searchClient is null)
        {
            return;
        }

        await _searchClient.DeleteDocumentsAsync(
            "Id",
            new[] { documentId },
            cancellationToken: cancellationToken);

        LogDocumentDeleted(_logger, documentId);
    }

    public async Task<IReadOnlyList<SearchResult<SessionSearchDocument>>> SearchAsync(
        string queryText,
        float[] queryVector,
        string? patientIdFilter,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (!_isConfigured || _searchClient is null)
        {
            LogSearchSkipped(_logger);
            return Array.Empty<SearchResult<SessionSearchDocument>>();
        }

        var searchOptions = new AzureSearchOptions
        {
            Size = maxResults,
            Select = { "Id", "SessionId", "PatientId", "SessionDate", "SessionType", "Content", "Summary", "RiskLevel" }
        };

        if (!string.IsNullOrEmpty(patientIdFilter))
        {
            if (!Guid.TryParse(patientIdFilter, out var sanitizedGuid))
                throw new ArgumentException("patientIdFilter must be a valid GUID", nameof(patientIdFilter));
            searchOptions.Filter = $"PatientId eq '{sanitizedGuid:D}'";
        }

        if (queryVector.Length > 0)
        {
            searchOptions.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = maxResults,
                        Fields = { "ContentVector" }
                    }
                }
            };
        }

        LogSearchExecuting(_logger, queryText.Length, maxResults, patientIdFilter ?? "(all)");

        var response = await _searchClient.SearchAsync<SessionSearchDocument>(
            queryText,
            searchOptions,
            cancellationToken);

        var results = new List<SearchResult<SessionSearchDocument>>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(result);
        }

        LogSearchCompleted(_logger, results.Count);
        return results;
    }

    private SearchIndex BuildIndexDefinition()
    {
        return new SearchIndex(_options.IndexName)
        {
            Fields = new FieldBuilder().Build(typeof(SessionSearchDocument)),
            VectorSearch = new VectorSearch
            {
                Profiles =
                {
                    new VectorSearchProfile("vector-profile", "hnsw-config")
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration("hnsw-config")
                    {
                        Parameters = new HnswParameters
                        {
                            Metric = VectorSearchAlgorithmMetric.Cosine,
                            M = 4,
                            EfConstruction = 400,
                            EfSearch = 500
                        }
                    }
                }
            }
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SearchIndexService configured with Endpoint={Endpoint}, IndexName={IndexName}")]
    private static partial void LogConfigurationValues(ILogger logger, string endpoint, string indexName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure Search is not configured. Search functionality will be unavailable. Set AzureSearch:Endpoint to enable.")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Indexing skipped for document {DocumentId} - search service not configured")]
    private static partial void LogIndexingSkipped(ILogger logger, string documentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Search index {IndexName} is ready")]
    private static partial void LogIndexReady(ILogger logger, string indexName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed document {DocumentId}")]
    private static partial void LogDocumentIndexed(ILogger logger, string documentId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed {Count} documents")]
    private static partial void LogDocumentsIndexed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted document {DocumentId}")]
    private static partial void LogDocumentDeleted(ILogger logger, string documentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search skipped - search service not configured")]
    private static partial void LogSearchSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing search: queryLength={QueryLength}, maxResults={MaxResults}, patientFilter={PatientFilter}")]
    private static partial void LogSearchExecuting(ILogger logger, int queryLength, int maxResults, string patientFilter);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search returned {Count} results")]
    private static partial void LogSearchCompleted(ILogger logger, int count);
}
