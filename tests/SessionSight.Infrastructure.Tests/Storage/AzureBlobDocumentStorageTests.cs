using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Moq;
using SessionSight.Infrastructure.Storage;

namespace SessionSight.Infrastructure.Tests.Storage;

public class AzureBlobDocumentStorageTests
{
    private readonly Mock<BlobServiceClient> _blobServiceClient = new();
    private readonly Mock<BlobContainerClient> _containerClient = new();
    private readonly Mock<BlobClient> _blobClient = new();
    private readonly AzureBlobDocumentStorage _sut;
    private string? _capturedBlobPath;

    public AzureBlobDocumentStorageTests()
    {
        _blobServiceClient
            .Setup(x => x.GetBlobContainerClient("session-documents"))
            .Returns(_containerClient.Object);

        // Capture the blob path passed to GetBlobClient
        _containerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Callback<string>(path => _capturedBlobPath = path)
            .Returns(_blobClient.Object);

        // Setup DownloadStreamingAsync (both overloads)
        var streamingResult = BlobsModelFactory.BlobDownloadStreamingResult(
            content: new MemoryStream(new byte[] { 1, 2, 3 }));
        var downloadResponse = new Mock<Response<BlobDownloadStreamingResult>>();
        downloadResponse.Setup(x => x.Value).Returns(streamingResult);

        _blobClient
            .Setup(x => x.DownloadStreamingAsync(
                It.IsAny<Azure.HttpRange>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResponse.Object);

        _blobClient
            .Setup(x => x.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResponse.Object);

        // Setup DeleteIfExistsAsync
        var deleteResponse = new Mock<Response<bool>>();
        deleteResponse.Setup(x => x.Value).Returns(true);
        _blobClient
            .Setup(x => x.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResponse.Object);

        _sut = new AzureBlobDocumentStorage(_blobServiceClient.Object);
    }

    [Theory]
    [InlineData(
        "https://storage.blob.core.windows.net/session-documents/abc-123/Therapy%20Note%20(Session%201).pdf",
        "abc-123/Therapy Note (Session 1).pdf")]
    [InlineData(
        "https://storage.blob.core.windows.net/session-documents/abc-123/simple-file.pdf",
        "abc-123/simple-file.pdf")]
    [InlineData(
        "https://storage.blob.core.windows.net/session-documents/abc-123/file%20with%20spaces.pdf",
        "abc-123/file with spaces.pdf")]
    [InlineData(
        "https://storage.blob.core.windows.net/session-documents/abc-123/report%20%2B%20notes.pdf",
        "abc-123/report + notes.pdf")]
    public async Task DownloadAsync_DecodesUriEncodedBlobPath(string blobUri, string expectedBlobPath)
    {
        await _sut.DownloadAsync(blobUri);

        _capturedBlobPath.Should().Be(expectedBlobPath);
    }

    [Theory]
    [InlineData(
        "https://storage.blob.core.windows.net/session-documents/abc-123/Therapy%20Note%20(Session%201).pdf",
        "abc-123/Therapy Note (Session 1).pdf")]
    [InlineData(
        "https://storage.blob.core.windows.net/session-documents/abc-123/simple-file.pdf",
        "abc-123/simple-file.pdf")]
    [InlineData(
        "https://storage.blob.core.windows.net/session-documents/abc-123/file%20with%20spaces.pdf",
        "abc-123/file with spaces.pdf")]
    public async Task DeleteAsync_DecodesUriEncodedBlobPath(string blobUri, string expectedBlobPath)
    {
        await _sut.DeleteAsync(blobUri);

        _capturedBlobPath.Should().Be(expectedBlobPath);
    }

    [Fact]
    public async Task DownloadAsync_PlainFilename_WorksWithoutDecoding()
    {
        const string blobUri = "https://storage.blob.core.windows.net/session-documents/abc-123/notes.pdf";

        var result = await _sut.DownloadAsync(blobUri);

        result.Should().NotBeNull();
        _capturedBlobPath.Should().Be("abc-123/notes.pdf");
    }
}
