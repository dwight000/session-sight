namespace SessionSight.Core.Interfaces;

public interface IDocumentStorage
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType);
    Task<Stream> DownloadAsync(string blobUri);
    Task DeleteAsync(string blobUri);
}
