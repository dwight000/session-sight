using FluentAssertions;
using SessionSight.Agents.Services;
using SessionSight.Core.Exceptions;

namespace SessionSight.Agents.Tests.Services;

public class DocumentIntelligenceParserTests
{
    [Theory]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".doc")]
    [InlineData(".jpeg")]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".tiff")]
    [InlineData(".tif")]
    [InlineData(".bmp")]
    [InlineData(".PDF")]
    [InlineData(".Docx")]
    public void ValidateFileFormat_SupportedExtension_DoesNotThrow(string extension)
    {
        // For PDF, supply valid magic bytes; for others, any bytes are fine
        var bytes = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "%PDF-1.4 test content"u8.ToArray()
            : new byte[] { 0x01, 0x02, 0x03 };

        var act = () => DocumentIntelligenceParser.ValidateFileFormat($"file{extension}", bytes);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".html")]
    [InlineData(".csv")]
    [InlineData(".xlsx")]
    [InlineData(".xml")]
    [InlineData("")]
    public void ValidateFileFormat_UnsupportedExtension_ThrowsDocumentValidationException(string extension)
    {
        var fileName = string.IsNullOrEmpty(extension) ? "noextension" : $"file{extension}";
        var bytes = new byte[] { 0x01, 0x02, 0x03 };

        var act = () => DocumentIntelligenceParser.ValidateFileFormat(fileName, bytes);

        act.Should().Throw<DocumentValidationException>()
            .WithMessage("*Unsupported file format*");
    }

    [Fact]
    public void ValidateFileFormat_PdfWithInvalidMagicBytes_ThrowsDocumentValidationException()
    {
        var corruptBytes = "This is not a PDF"u8.ToArray();

        var act = () => DocumentIntelligenceParser.ValidateFileFormat("document.pdf", corruptBytes);

        act.Should().Throw<DocumentValidationException>()
            .WithMessage("*does not appear to be a valid PDF*");
    }

    [Fact]
    public void ValidateFileFormat_PdfWithTooFewBytes_ThrowsDocumentValidationException()
    {
        var tinyBytes = new byte[] { 0x25 }; // Just '%', not enough for '%PDF'

        var act = () => DocumentIntelligenceParser.ValidateFileFormat("test.pdf", tinyBytes);

        act.Should().Throw<DocumentValidationException>()
            .WithMessage("*does not appear to be a valid PDF*");
    }

    [Fact]
    public void ValidateFileFormat_PdfWithValidMagicBytes_DoesNotThrow()
    {
        var validPdf = "%PDF-1.7 some content"u8.ToArray();

        var act = () => DocumentIntelligenceParser.ValidateFileFormat("report.pdf", validPdf);

        act.Should().NotThrow();
    }
}
