using SessionSight.Agents.Models;

namespace SessionSight.Agents.Services;

/// <summary>
/// Contract for parsing documents into structured content.
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Parses a document stream and extracts structured content.
    /// </summary>
    /// <param name="document">The document stream to parse.</param>
    /// <param name="fileName">Original filename for format detection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ParsedDocument containing extracted text and structure.</returns>
    Task<ParsedDocument> ParseAsync(Stream document, string fileName, CancellationToken ct = default);
}
