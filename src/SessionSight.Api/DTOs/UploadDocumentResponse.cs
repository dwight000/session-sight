namespace SessionSight.Api.DTOs;

public record UploadDocumentResponse(
    Guid DocumentId,
    Guid SessionId,
    string OriginalFileName,
    string BlobUri,
    string Status);
