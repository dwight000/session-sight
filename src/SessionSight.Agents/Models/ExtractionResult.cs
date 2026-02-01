using SessionSight.Core.Schema;

namespace SessionSight.Agents.Models;

/// <summary>
/// Result from the Clinical Extractor Agent's processing of a therapy note.
/// </summary>
public class ExtractionResult
{
    /// <summary>
    /// Unique session identifier.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The extracted clinical data.
    /// </summary>
    public ClinicalExtraction Data { get; set; } = new();

    /// <summary>
    /// Overall confidence score (0.0-1.0) across all extracted fields.
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Whether this extraction requires human review.
    /// True if validation failed or risk fields have low confidence.
    /// </summary>
    public bool RequiresReview { get; set; }

    /// <summary>
    /// List of fields with low confidence scores.
    /// </summary>
    public List<string> LowConfidenceFields { get; set; } = new();

    /// <summary>
    /// The models used for extraction (different sections may use different models).
    /// </summary>
    public List<string> ModelsUsed { get; set; } = new();

    /// <summary>
    /// Any errors encountered during extraction.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Number of tool calls made by the agent during extraction.
    /// </summary>
    public int ToolCallCount { get; set; }

    /// <summary>
    /// Whether the extraction completed successfully.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;
}
