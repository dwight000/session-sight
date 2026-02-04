using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        if (string.IsNullOrEmpty(_options.Endpoint))
        {
            LogNotConfigured(_logger);
            _isConfigured = false;
            return;
        }

        _isConfigured = true;
        var endpoint = new Uri(_options.Endpoint);
        var credential = new DefaultAzureCredential();

        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, _options.IndexName, credential);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure Search is not configured. Search functionality will be unavailable. Set AzureSearch:Endpoint to enable.")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Search index {IndexName} is ready")]
    private static partial void LogIndexReady(ILogger logger, string indexName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed document {DocumentId}")]
    private static partial void LogDocumentIndexed(ILogger logger, string documentId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed {Count} documents")]
    private static partial void LogDocumentsIndexed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted document {DocumentId}")]
    private static partial void LogDocumentDeleted(ILogger logger, string documentId);
}
