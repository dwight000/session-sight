using System.Text.Json.Serialization;

namespace SessionSight.Api.DTOs;

/// <summary>
/// Request for processing a therapy note from blob storage.
/// Used by blob trigger function to initiate extraction.
/// </summary>
public record ProcessNoteRequest(
    /// <summary>
    /// External patient identifier.
    /// </summary>
    string PatientId,

    /// <summary>
    /// URI of the blob to process.
    /// </summary>
    string BlobUri,

    /// <summary>
    /// Date of the therapy session.
    /// </summary>
    [property: JsonRequired] DateOnly SessionDate,

    /// <summary>
    /// Original filename.
    /// </summary>
    string FileName
);

/// <summary>
/// Response after accepting a note for processing.
/// </summary>
public record ProcessNoteResponse(
    /// <summary>
    /// The created session ID.
    /// </summary>
    Guid SessionId,

    /// <summary>
    /// Message indicating processing status.
    /// </summary>
    string Message
);
