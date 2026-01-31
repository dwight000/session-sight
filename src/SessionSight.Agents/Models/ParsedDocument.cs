namespace SessionSight.Agents.Models;

/// <summary>
/// Represents a document parsed by Document Intelligence.
/// Contains the raw and structured content extracted from the document.
/// </summary>
public class ParsedDocument
{
    /// <summary>
    /// Raw text content extracted from the document.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Content converted to markdown format.
    /// </summary>
    public string MarkdownContent { get; set; } = string.Empty;

    /// <summary>
    /// Structural sections identified in the document.
    /// </summary>
    public List<DocumentSection> Sections { get; set; } = new();

    /// <summary>
    /// Document-level metadata (page count, format, etc.).
    /// </summary>
    public ParsedDocumentMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Represents a structural section within a parsed document.
/// </summary>
public class DocumentSection
{
    /// <summary>
    /// Section heading or title, if present.
    /// </summary>
    public string? Heading { get; set; }

    /// <summary>
    /// Text content of this section.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Starting page number (1-indexed).
    /// </summary>
    public int StartPage { get; set; }

    /// <summary>
    /// Ending page number (1-indexed).
    /// </summary>
    public int EndPage { get; set; }
}

/// <summary>
/// Metadata about the parsed document.
/// </summary>
public class ParsedDocumentMetadata
{
    /// <summary>
    /// Total page count.
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// Original file format (pdf, docx, etc.).
    /// </summary>
    public string FileFormat { get; set; } = string.Empty;

    /// <summary>
    /// Size of the original file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Confidence score from Document Intelligence (0.0 to 1.0).
    /// </summary>
    public double ExtractionConfidence { get; set; }
}
