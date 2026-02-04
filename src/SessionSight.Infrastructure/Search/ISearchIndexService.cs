namespace SessionSight.Infrastructure.Search;

/// <summary>
/// Service for managing the Azure AI Search index.
/// </summary>
public interface ISearchIndexService
{
    /// <summary>
    /// Ensures the search index exists with the correct schema.
    /// Creates the index if it doesn't exist.
    /// </summary>
    Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a session document for search.
    /// </summary>
    Task IndexDocumentAsync(SessionSearchDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple session documents in a batch.
    /// </summary>
    Task IndexDocumentsAsync(IEnumerable<SessionSearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the search index.
    /// </summary>
    Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}
