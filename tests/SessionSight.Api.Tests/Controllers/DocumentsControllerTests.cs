using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;

namespace SessionSight.Api.Tests.Controllers;

public class DocumentsControllerTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IDocumentStorage> _documentStorageMock;
    private readonly DocumentsController _controller;

    public DocumentsControllerTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _documentStorageMock = new Mock<IDocumentStorage>();
        _controller = new DocumentsController(_sessionRepositoryMock.Object, _documentStorageMock.Object);
    }

    [Fact]
    public async Task UploadDocument_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync((Session?)null);

        var result = await _controller.UploadDocument(sessionId, CreateMockFile());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UploadDocument_DocumentAlreadyExists_ReturnsConflict()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Id = Guid.NewGuid() }
        };
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);

        var result = await _controller.UploadDocument(sessionId, CreateMockFile());

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UploadDocument_ValidRequest_ReturnsCreated()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Document = null };
        var blobUri = "https://storage.blob.core.windows.net/docs/test.pdf";

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _documentStorageMock.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(blobUri);

        var file = CreateMockFile("therapy-note.pdf", "application/pdf");
        var result = await _controller.UploadDocument(sessionId, file);

        result.Result.Should().BeOfType<CreatedResult>();
        var createdResult = (CreatedResult)result.Result!;
        var response = (UploadDocumentResponse)createdResult.Value!;
        response.SessionId.Should().Be(sessionId);
        response.OriginalFileName.Should().Be("therapy-note.pdf");
        response.BlobUri.Should().Be(blobUri);
        response.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task UploadDocument_ValidRequest_UploadsToStorage()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Document = null };

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _documentStorageMock.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("uri");

        var file = CreateMockFile("test.pdf", "application/pdf");
        await _controller.UploadDocument(sessionId, file);

        _documentStorageMock.Verify(s => s.UploadAsync("test.pdf", It.IsAny<Stream>(), "application/pdf"), Times.Once);
    }

    [Fact]
    public async Task UploadDocument_ValidRequest_AddsDocumentToSession()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Document = null };

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);
        _documentStorageMock.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("uri");

        await _controller.UploadDocument(sessionId, CreateMockFile());

        _sessionRepositoryMock.Verify(r => r.AddDocumentAsync(session, It.Is<SessionDocument>(d =>
            d.SessionId == sessionId &&
            d.Status == DocumentStatus.Pending)), Times.Once);
    }

    [Fact]
    public async Task GetExtraction_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync((Session?)null);

        var result = await _controller.GetExtraction(sessionId);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetExtraction_NoExtractionResult_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Extraction = null };
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);

        var result = await _controller.GetExtraction(sessionId);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetExtraction_WithExtractionResult_ReturnsOk()
    {
        var sessionId = Guid.NewGuid();
        var extractionId = Guid.NewGuid();
        var extraction = new ExtractionResult
        {
            Id = extractionId,
            SessionId = sessionId,
            SchemaVersion = "1.0.0",
            ModelUsed = "gpt-4o",
            OverallConfidence = 0.9,
            RequiresReview = false,
            ExtractedAt = DateTime.UtcNow,
            Data = new ClinicalExtraction()
        };
        var session = new Session { Id = sessionId, Extraction = extraction };
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId))
            .ReturnsAsync(session);

        var result = await _controller.GetExtraction(sessionId);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var dto = (ExtractionResultDto)okResult.Value!;
        dto.Id.Should().Be(extractionId);
        dto.SessionId.Should().Be(sessionId);
        dto.ModelUsed.Should().Be("gpt-4o");
    }

    private static IFormFile CreateMockFile(string fileName = "test.pdf", string contentType = "application/pdf")
    {
        var fileMock = new Mock<IFormFile>();
        var content = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF magic bytes
        var stream = new MemoryStream(content);

        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        return fileMock.Object;
    }
}
