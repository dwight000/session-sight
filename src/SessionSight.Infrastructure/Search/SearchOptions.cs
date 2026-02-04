namespace SessionSight.Infrastructure.Search;

/// <summary>
/// Configuration options for Azure AI Search.
/// </summary>
public class SearchOptions
{
    public const string SectionName = "AzureSearch";

    /// <summary>
    /// Azure AI Search endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Name of the search index for session documents.
    /// </summary>
    public string IndexName { get; set; } = "sessionsight-sessions";

    /// <summary>
    /// Vector dimensions for embeddings. Default: 3072 (text-embedding-3-large).
    /// </summary>
    public int VectorDimensions { get; set; } = 3072;
}
