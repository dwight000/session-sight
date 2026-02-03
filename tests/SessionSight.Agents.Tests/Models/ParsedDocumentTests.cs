using FluentAssertions;
using SessionSight.Agents.Models;

namespace SessionSight.Agents.Tests.Models;

public class ParsedDocumentTests
{
    [Fact]
    public void ParsedDocument_DefaultValues_AreInitialized()
    {
        var doc = new ParsedDocument();

        doc.Content.Should().BeEmpty();
        doc.MarkdownContent.Should().BeEmpty();
        doc.Sections.Should().NotBeNull();
        doc.Sections.Should().BeEmpty();
        doc.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void ParsedDocument_CanSetContent()
    {
        var doc = new ParsedDocument
        {
            Content = "Raw text content",
            MarkdownContent = "# Markdown\n\nContent"
        };

        doc.Content.Should().Be("Raw text content");
        doc.MarkdownContent.Should().Be("# Markdown\n\nContent");
    }

    [Fact]
    public void ParsedDocument_CanAddSections()
    {
        var doc = new ParsedDocument();
        doc.Sections.Add(new DocumentSection { Heading = "Section 1", Content = "Content 1" });
        doc.Sections.Add(new DocumentSection { Heading = "Section 2", Content = "Content 2" });

        doc.Sections.Should().HaveCount(2);
    }

    [Fact]
    public void DocumentSection_DefaultValues_AreInitialized()
    {
        var section = new DocumentSection();

        section.Heading.Should().BeNull();
        section.Content.Should().BeEmpty();
        section.StartPage.Should().Be(0);
        section.EndPage.Should().Be(0);
    }

    [Fact]
    public void DocumentSection_CanSetAllProperties()
    {
        var section = new DocumentSection
        {
            Heading = "Assessment",
            Content = "Patient presents with anxiety...",
            StartPage = 1,
            EndPage = 2
        };

        section.Heading.Should().Be("Assessment");
        section.Content.Should().Be("Patient presents with anxiety...");
        section.StartPage.Should().Be(1);
        section.EndPage.Should().Be(2);
    }

    [Fact]
    public void ParsedDocumentMetadata_DefaultValues_AreInitialized()
    {
        var metadata = new ParsedDocumentMetadata();

        metadata.PageCount.Should().Be(0);
        metadata.FileFormat.Should().BeEmpty();
        metadata.FileSizeBytes.Should().Be(0);
        metadata.ExtractionConfidence.Should().Be(0.0);
    }

    [Fact]
    public void ParsedDocumentMetadata_CanSetAllProperties()
    {
        var metadata = new ParsedDocumentMetadata
        {
            PageCount = 5,
            FileFormat = "pdf",
            FileSizeBytes = 1024000,
            ExtractionConfidence = 0.95
        };

        metadata.PageCount.Should().Be(5);
        metadata.FileFormat.Should().Be("pdf");
        metadata.FileSizeBytes.Should().Be(1024000);
        metadata.ExtractionConfidence.Should().Be(0.95);
    }

    [Fact]
    public void ParsedDocument_WithMetadata_StoresCorrectly()
    {
        var doc = new ParsedDocument
        {
            Content = "Test content",
            Metadata = new ParsedDocumentMetadata
            {
                PageCount = 3,
                FileFormat = "docx",
                FileSizeBytes = 50000,
                ExtractionConfidence = 0.88
            }
        };

        doc.Metadata.PageCount.Should().Be(3);
        doc.Metadata.FileFormat.Should().Be("docx");
        doc.Metadata.FileSizeBytes.Should().Be(50000);
        doc.Metadata.ExtractionConfidence.Should().Be(0.88);
    }
}
