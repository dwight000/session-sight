using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SessionSight.Core.Interfaces;

namespace SessionSight.Infrastructure.Storage;

public class AzureBlobDocumentStorage : IDocumentStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private const string ContainerName = "session-documents";

    public AzureBlobDocumentStorage(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobName = $"{Guid.NewGuid()}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return blobClient.Uri.ToString();
    }

    public async Task<Stream> DownloadAsync(string blobUri)
    {
        var blobClient = new BlobClient(new Uri(blobUri));
        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    public async Task DeleteAsync(string blobUri)
    {
        var blobClient = new BlobClient(new Uri(blobUri));
        await blobClient.DeleteIfExistsAsync();
    }
}
