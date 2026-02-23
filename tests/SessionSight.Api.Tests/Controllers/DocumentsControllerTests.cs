using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using SessionSight.Agents.Services;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;
using SessionSight.Infrastructure.Search;
using Microsoft.Extensions.Logging;

namespace SessionSight.Api.Tests.Controllers;

public class DocumentsControllerTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IExtractionStepRepository> _stepRepositoryMock;
    private readonly Mock<IDocumentStorage> _documentStorageMock;
    private readonly Mock<ISearchIndexService> _searchIndexServiceMock;
    private readonly DocumentIntelligenceOptions _docOptions;
    private readonly DocumentsController _controller;

    public DocumentsControllerTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _stepRepositoryMock = new Mock<IExtractionStepRepository>();
        _documentStorageMock = new Mock<IDocumentStorage>();
        _searchIndexServiceMock = new Mock<ISearchIndexService>();
        _docOptions = new DocumentIntelligenceOptions();
        _controller = new DocumentsController(
            _sessionRepositoryMock.Object,
            _documentRepositoryMock.Object,
            _stepRepositoryMock.Object,
            _documentStorageMock.Object,
            _searchIndexServiceMock.Object,
            new Mock<ILogger<DocumentsController>>().Object,
            Options.Create(_docOptions));
    }

    [Fact]
    public async Task UploadDocument_EmptyFile_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid();
        var file = CreateMockFile(length: 0);

        var result = await _controller.UploadDocument(sessionId, file);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result.Result!;
        badRequest.Value.Should().Be("File is empty.");
    }

    [Fact]
    public async Task UploadDocument_FileTooLarge_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid();
        var file = CreateMockFile(length: _docOptions.MaxFileSizeBytes + 1);

        var result = await _controller.UploadDocument(sessionId, file);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result.Result!;
        ((string)badRequest.Value!).Should().Contain("exceeds maximum allowed");
    }

    [Theory]
    [InlineData("note.txt")]
    [InlineData("note.xml")]
    [InlineData("note.html")]
    [InlineData("note")]
    public async Task UploadDocument_UnsupportedFileType_ReturnsBadRequest(string fileName)
    {
        var sessionId = Guid.NewGuid();
        var file = CreateMockFile(fileName: fileName);

        var result = await _controller.UploadDocument(sessionId, file);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result.Result!;
        ((string)badRequest.Value!).Should().Contain("Unsupported file type");
    }

    [Fact]
    public async Task UploadDocument_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
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
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
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

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
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

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
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

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _documentStorageMock.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("uri");

        await _controller.UploadDocument(sessionId, CreateMockFile());

        _documentRepositoryMock.Verify(r => r.AddDocumentAsync(session, It.Is<SessionDocument>(d =>
            d.SessionId == sessionId &&
            d.Status == DocumentStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExtraction_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _controller.GetExtraction(sessionId);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetExtraction_NoExtractionResult_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Extraction = null };
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
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
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _controller.GetExtraction(sessionId);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var dto = (ExtractionResultDto)okResult.Value!;
        dto.Id.Should().Be(extractionId);
        dto.SessionId.Should().Be(sessionId);
        dto.ModelUsed.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task DeleteDocument_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _controller.DeleteDocument(sessionId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteDocument_NoDocument_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var session = new Session { Id = sessionId, Document = null };
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _controller.DeleteDocument(sessionId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteDocument_WithDocument_DeletesAndReturnsNoContent()
    {
        var sessionId = Guid.NewGuid();
        var blobUri = "https://storage.blob.core.windows.net/docs/test.pdf";
        var session = new Session
        {
            Id = sessionId,
            Document = new SessionDocument { Id = Guid.NewGuid(), BlobUri = blobUri }
        };
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _controller.DeleteDocument(sessionId);

        result.Should().BeOfType<NoContentResult>();
        _documentStorageMock.Verify(s => s.DeleteAsync(blobUri), Times.Once);
        _searchIndexServiceMock.Verify(s => s.DeleteDocumentAsync(sessionId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _documentRepositoryMock.Verify(r => r.DeleteDocumentAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IFormFile CreateMockFile(string fileName = "test.pdf", string contentType = "application/pdf", long? length = null)
    {
        var fileMock = new Mock<IFormFile>();
        var content = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF magic bytes
        var stream = new MemoryStream(content);

        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(length ?? content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        return fileMock.Object;
    }
}
