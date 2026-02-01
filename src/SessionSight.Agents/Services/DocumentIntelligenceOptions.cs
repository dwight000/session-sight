namespace SessionSight.Agents.Services;

/// <summary>
/// Configuration options for Azure Document Intelligence.
/// </summary>
public class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    /// <summary>
    /// Azure Document Intelligence endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Maximum file size allowed in bytes. Default: 50MB.
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Maximum number of pages to process. Default: 30.
    /// </summary>
    public int MaxPageCount { get; set; } = 30;
}
