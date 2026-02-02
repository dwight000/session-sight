using System.Text;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionSight.Agents.Models;

namespace SessionSight.Agents.Services;

/// <summary>
/// Parses documents using Azure Document Intelligence service.
/// Uses the prebuilt-layout model for text, table, and structure extraction.
/// </summary>
public partial class DocumentIntelligenceParser : IDocumentParser
{
    private readonly DocumentIntelligenceClient _client;
    private readonly DocumentIntelligenceOptions _options;
    private readonly ILogger<DocumentIntelligenceParser> _logger;

    public DocumentIntelligenceParser(
        DocumentIntelligenceClient client,
        IOptions<DocumentIntelligenceOptions> options,
        ILogger<DocumentIntelligenceParser> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ParsedDocument> ParseAsync(Stream document, string fileName, CancellationToken ct = default)
    {
        LogParsingDocument(_logger, fileName);

        // Read stream to memory for size validation and BinaryData conversion
        using var memoryStream = new MemoryStream();
        await document.CopyToAsync(memoryStream, ct);
        var documentBytes = memoryStream.ToArray();

        if (documentBytes.Length > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Document size ({documentBytes.Length:N0} bytes) exceeds maximum allowed ({_options.MaxFileSizeBytes:N0} bytes)");
        }

        var binaryData = BinaryData.FromBytes(documentBytes);

        // Use prebuilt-layout model for comprehensive text extraction
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            binaryData,
            cancellationToken: ct);

        var result = operation.Value;

        // Validate page count
        if (result.Pages.Count > _options.MaxPageCount)
        {
            throw new InvalidOperationException(
                $"Document page count ({result.Pages.Count}) exceeds maximum allowed ({_options.MaxPageCount})");
        }

        LogDocumentParsed(_logger, result.Pages.Count, result.Paragraphs?.Count ?? 0);

        return new ParsedDocument
        {
            Content = result.Content,
            MarkdownContent = ConvertToMarkdown(result),
            Sections = ExtractSections(result),
            Metadata = new ParsedDocumentMetadata
            {
                PageCount = result.Pages.Count,
                FileFormat = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
                FileSizeBytes = documentBytes.Length,
                ExtractionConfidence = CalculateConfidence(result)
            }
        };
    }

    private static string ConvertToMarkdown(AnalyzeResult result)
    {
        var markdown = new StringBuilder();

        if (result.Paragraphs is null || result.Paragraphs.Count == 0)
        {
            // Fallback to raw content if no paragraph structure
            return result.Content;
        }

        foreach (var paragraph in result.Paragraphs)
        {
            var role = paragraph.Role;

            if (role == ParagraphRole.Title)
            {
                markdown.Append("# ").AppendLine(paragraph.Content);
                markdown.AppendLine();
            }
            else if (role == ParagraphRole.SectionHeading)
            {
                markdown.Append("## ").AppendLine(paragraph.Content);
                markdown.AppendLine();
            }
            else if (role == ParagraphRole.PageHeader || role == ParagraphRole.PageFooter)
            {
                // Skip headers and footers for content extraction
                continue;
            }
            else
            {
                markdown.AppendLine(paragraph.Content);
                markdown.AppendLine();
            }
        }

        // Append tables if present
        if (result.Tables is not null)
        {
            foreach (var table in result.Tables)
            {
                markdown.AppendLine();
                markdown.AppendLine(ConvertTableToMarkdown(table));
                markdown.AppendLine();
            }
        }

        return markdown.ToString().Trim();
    }

    private static string ConvertTableToMarkdown(DocumentTable table)
    {
        var markdown = new StringBuilder();
        var rows = new Dictionary<int, List<(int Col, string Content)>>();

        // Group cells by row
        foreach (var cell in table.Cells)
        {
            if (!rows.TryGetValue(cell.RowIndex, out var rowCells))
            {
                rowCells = new List<(int Col, string Content)>();
                rows[cell.RowIndex] = rowCells;
            }
            rowCells.Add((cell.ColumnIndex, cell.Content));
        }

        // Build markdown table
        foreach (var rowIndex in rows.Keys.OrderBy(r => r))
        {
            var cells = rows[rowIndex].OrderBy(c => c.Col).Select(c => c.Content);
            markdown.Append("| ").Append(string.Join(" | ", cells)).AppendLine(" |");

            // Add header separator after first row
            if (rowIndex == 0)
            {
                var separator = string.Join(" | ", Enumerable.Repeat("---", table.ColumnCount));
                markdown.Append("| ").Append(separator).AppendLine(" |");
            }
        }

        return markdown.ToString();
    }

    private static List<Models.DocumentSection> ExtractSections(AnalyzeResult result)
    {
        var sections = new List<Models.DocumentSection>();

        if (result.Paragraphs is null || result.Paragraphs.Count == 0)
        {
            // If no structured paragraphs, create a single section with all content
            sections.Add(new Models.DocumentSection
            {
                Heading = null,
                Content = result.Content,
                StartPage = 1,
                EndPage = result.Pages.Count
            });
            return sections;
        }

        Models.DocumentSection? currentSection = null;
        var currentContent = new StringBuilder();

        foreach (var paragraph in result.Paragraphs)
        {
            var isHeading = paragraph.Role == ParagraphRole.Title ||
                           paragraph.Role == ParagraphRole.SectionHeading;

            if (isHeading)
            {
                // Save previous section if exists
                if (currentSection is not null)
                {
                    currentSection.Content = currentContent.ToString().Trim();
                    sections.Add(currentSection);
                }

                // Start new section
                var pageNumber = GetPageNumber(paragraph, result);
                currentSection = new Models.DocumentSection
                {
                    Heading = paragraph.Content,
                    StartPage = pageNumber,
                    EndPage = pageNumber
                };
                currentContent.Clear();
            }
            else if (paragraph.Role != ParagraphRole.PageHeader &&
                     paragraph.Role != ParagraphRole.PageFooter)
            {
                // Add content to current section
                if (currentSection is null)
                {
                    var pageNumber = GetPageNumber(paragraph, result);
                    currentSection = new Models.DocumentSection
                    {
                        Heading = null,
                        StartPage = pageNumber,
                        EndPage = pageNumber
                    };
                }

                currentContent.AppendLine(paragraph.Content);
                currentSection.EndPage = GetPageNumber(paragraph, result);
            }
        }

        // Don't forget the last section
        if (currentSection is not null)
        {
            currentSection.Content = currentContent.ToString().Trim();
            sections.Add(currentSection);
        }

        return sections;
    }

    private static int GetPageNumber(DocumentParagraph paragraph, AnalyzeResult result)
    {
        if (paragraph.BoundingRegions is null || paragraph.BoundingRegions.Count == 0)
        {
            return 1;
        }

        return paragraph.BoundingRegions[0].PageNumber;
    }

    private static double CalculateConfidence(AnalyzeResult result)
    {
        if (result.Pages.Count == 0)
        {
            return 0.0;
        }

        // Calculate average word confidence across all pages
        var totalConfidence = 0.0;
        var wordCount = 0;

        foreach (var page in result.Pages)
        {
            if (page.Words is not null)
            {
                foreach (var word in page.Words)
                {
                    totalConfidence += word.Confidence;
                    wordCount++;
                }
            }
        }

        return wordCount > 0 ? totalConfidence / wordCount : 0.95;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Parsing document: {FileName}")]
    private static partial void LogParsingDocument(ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Document parsed: {PageCount} pages, {ParagraphCount} paragraphs")]
    private static partial void LogDocumentParsed(ILogger logger, int pageCount, int paragraphCount);
}
