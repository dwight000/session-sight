namespace SessionSight.Agents.Models;

/// <summary>
/// Result from the Intake Agent's processing of a document.
/// Contains validation status and extracted metadata.
/// </summary>
public class IntakeResult
{
    /// <summary>
    /// The original parsed document.
    /// </summary>
    public ParsedDocument Document { get; set; } = new();

    /// <summary>
    /// Metadata extracted by the LLM from document content.
    /// </summary>
    public ExtractedMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Whether the document appears to be a valid therapy session note.
    /// </summary>
    public bool IsValidTherapyNote { get; set; }

    /// <summary>
    /// If not valid, explains why the document was rejected.
    /// </summary>
    public string? ValidationError { get; set; }

    /// <summary>
    /// The model used for this intake (e.g., "gpt-4o-mini").
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;
}

/// <summary>
/// Metadata extracted from the document content by the LLM.
/// </summary>
public class ExtractedMetadata
{
    /// <summary>
    /// Detected document type (e.g., "Session Note", "Progress Report", "Assessment").
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Session date if identifiable from the document.
    /// </summary>
    public DateOnly? SessionDate { get; set; }

    /// <summary>
    /// Patient/client identifier if present in the document.
    /// </summary>
    public string? PatientId { get; set; }

    /// <summary>
    /// Therapist/clinician name if present.
    /// </summary>
    public string? TherapistName { get; set; }

    /// <summary>
    /// Primary language of the document content.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Estimated word count of the document.
    /// </summary>
    public int EstimatedWordCount { get; set; }
}
