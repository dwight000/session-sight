using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SessionSight.BlobTrigger.Functions;

/// <summary>
/// Azure Function triggered when a new file is dropped in the incoming container.
/// Validates the file, moves it to processing, and triggers extraction.
/// </summary>
public partial class ProcessIncomingNoteFunction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<ProcessIncomingNoteFunction> _logger;

    private const long MaxFileSizeBytes = 50_000_000; // 50MB

    public ProcessIncomingNoteFunction(
        IHttpClientFactory httpClientFactory,
        BlobServiceClient blobServiceClient,
        ILogger<ProcessIncomingNoteFunction> logger)
    {
        _httpClientFactory = httpClientFactory;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Processes an incoming therapy note blob.
    /// Path format: incoming/{patientId}/{fileName}
    /// </summary>
    [Function("ProcessIncomingNote")]
    public async Task Run(
        [BlobTrigger("incoming/{patientId}/{fileName}", Connection = "StorageConnection")]
        BlobClient blobClient,
        string patientId,
        string fileName,
        FunctionContext context)
    {
        LogProcessingBlob(_logger, patientId, fileName);

        try
        {
            // 1. Validate file exists and check size
            var properties = await blobClient.GetPropertiesAsync();
            var fileSize = properties.Value.ContentLength;

            if (fileSize > MaxFileSizeBytes)
            {
                LogFileTooLarge(_logger, fileSize, MaxFileSizeBytes);
                await MoveBlobAsync(blobClient, "failed", patientId, fileName, "File exceeds 50MB limit");
                return;
            }

            // 2. Validate file type
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var validExtensions = new[] { ".pdf", ".doc", ".docx", ".txt", ".rtf" };
            if (!validExtensions.Contains(extension, StringComparer.Ordinal))
            {
                LogInvalidFileType(_logger, extension);
                await MoveBlobAsync(blobClient, "failed", patientId, fileName, $"Invalid file type: {extension}");
                return;
            }

            // 3. Move to processing container
            var processingUri = await MoveBlobAsync(blobClient, "processing", patientId, fileName);
            if (processingUri is null)
            {
                LogFailedToMoveBlob(_logger);
                return;
            }

            // 4. Parse session date from filename
            var sessionDate = ParseDateFromFileName(fileName);

            // 5. Call the API to process the note
            var httpClient = _httpClientFactory.CreateClient("SessionSightApi");

            var request = new ProcessNoteRequest(
                PatientId: patientId,
                BlobUri: processingUri,
                SessionDate: sessionDate,
                FileName: fileName
            );

            var response = await httpClient.PostAsJsonAsync("/api/ingestion/process", request);

            if (response.IsSuccessStatusCode)
            {
                LogSuccessfullySubmitted(_logger, patientId, fileName);

                // Move to processed on success
                var processingClient = _blobServiceClient.GetBlobContainerClient("processing")
                    .GetBlobClient($"{patientId}/{fileName}");
                await MoveBlobAsync(processingClient, "processed", patientId, fileName);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                LogApiCallFailed(_logger, response.StatusCode, errorContent);

                // Move to failed
                var processingClient = _blobServiceClient.GetBlobContainerClient("processing")
                    .GetBlobClient($"{patientId}/{fileName}");
                await MoveBlobAsync(processingClient, "failed", patientId, fileName, errorContent);
            }
        }
        catch (Exception ex)
        {
            LogBlobProcessingError(_logger, ex, patientId, fileName);
            await MoveBlobAsync(blobClient, "failed", patientId, fileName, ex.Message);
        }
    }

    /// <summary>
    /// Moves a blob to a different container.
    /// </summary>
    private async Task<string?> MoveBlobAsync(
        BlobClient sourceClient,
        string targetContainer,
        string patientId,
        string fileName,
        string? errorReason = null)
    {
        try
        {
            var targetContainerClient = _blobServiceClient.GetBlobContainerClient(targetContainer);

            // Ensure target container exists
            await targetContainerClient.CreateIfNotExistsAsync();

            // Build target blob name (optionally with timestamp prefix for failed)
            var targetBlobName = $"{patientId}/{fileName}";
            if (targetContainer == "failed" && !string.IsNullOrEmpty(errorReason))
            {
                // Add timestamp to prevent overwrites in failed container
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                targetBlobName = $"{patientId}/{timestamp}_{fileName}";
            }

            var targetClient = targetContainerClient.GetBlobClient(targetBlobName);

            // Copy blob to target
            await targetClient.StartCopyFromUriAsync(sourceClient.Uri);

            // Wait for copy to complete
            var properties = await targetClient.GetPropertiesAsync();
            while (properties.Value.CopyStatus == Azure.Storage.Blobs.Models.CopyStatus.Pending)
            {
                await Task.Delay(100);
                properties = await targetClient.GetPropertiesAsync();
            }

            if (properties.Value.CopyStatus != Azure.Storage.Blobs.Models.CopyStatus.Success)
            {
                LogCopyFailed(_logger, properties.Value.CopyStatus);
                return null;
            }

            // Delete source blob
            await sourceClient.DeleteIfExistsAsync();

            LogBlobMoved(_logger, sourceClient.Uri, targetClient.Uri);
            return targetClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            LogMoveError(_logger, ex, targetContainer);
            return null;
        }
    }

    /// <summary>
    /// Parses session date from filename.
    /// Supports formats: YYYY-MM-DD, YYYYMMDD in the filename.
    /// Falls back to today's date if no date found.
    /// </summary>
    private DateOnly ParseDateFromFileName(string fileName)
    {
        // Try YYYY-MM-DD format
        var match = DatePatternWithDashes().Match(fileName);
        if (match.Success)
        {
            return DateOnly.Parse(match.Value, CultureInfo.InvariantCulture);
        }

        // Try YYYYMMDD format
        match = DatePatternNoDashes().Match(fileName);
        if (match.Success)
        {
            var dateStr = match.Value;
            return new DateOnly(
                int.Parse(dateStr[..4], CultureInfo.InvariantCulture),
                int.Parse(dateStr[4..6], CultureInfo.InvariantCulture),
                int.Parse(dateStr[6..8], CultureInfo.InvariantCulture));
        }

        // Default to today
        LogDateParseWarning(_logger, fileName);
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex DatePatternWithDashes();

    [GeneratedRegex(@"\d{8}")]
    private static partial Regex DatePatternNoDashes();

    /// <summary>
    /// Request DTO matching the API endpoint.
    /// </summary>
    private sealed record ProcessNoteRequest(
        string PatientId,
        string BlobUri,
        DateOnly SessionDate,
        string FileName
    );

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing blob: {PatientId}/{FileName}")]
    private static partial void LogProcessingBlob(ILogger logger, string patientId, string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File too large: {Size} bytes (max: {MaxSize}). Moving to failed.")]
    private static partial void LogFileTooLarge(ILogger logger, long size, long maxSize);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid file type: {Extension}. Moving to failed.")]
    private static partial void LogInvalidFileType(ILogger logger, string extension);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to move blob to processing container")]
    private static partial void LogFailedToMoveBlob(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully submitted note for processing. PatientId: {PatientId}, File: {FileName}")]
    private static partial void LogSuccessfullySubmitted(ILogger logger, string patientId, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "API call failed with status {StatusCode}: {Error}")]
    private static partial void LogApiCallFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing blob {PatientId}/{FileName}")]
    private static partial void LogBlobProcessingError(ILogger logger, Exception exception, string patientId, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to copy blob. Status: {Status}")]
    private static partial void LogCopyFailed(ILogger logger, Azure.Storage.Blobs.Models.CopyStatus status);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Moved blob from {Source} to {Target}")]
    private static partial void LogBlobMoved(ILogger logger, Uri source, Uri target);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error moving blob to {Container}")]
    private static partial void LogMoveError(ILogger logger, Exception exception, string container);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not parse date from filename '{FileName}', using today")]
    private static partial void LogDateParseWarning(ILogger logger, string fileName);
}
