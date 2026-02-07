using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Core.Entities;

public class ExtractionResult
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public string SchemaVersion { get; set; } = "1.0.0";
    public string ModelUsed { get; set; } = string.Empty;
    public double OverallConfidence { get; set; }
    public bool RequiresReview { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.NotFlagged;
    public List<string> ReviewReasons { get; set; } = new();
    public DateTime ExtractedAt { get; set; }
    public ClinicalExtraction Data { get; set; } = new();

    /// <summary>
    /// Session summary stored as JSON string.
    /// </summary>
    public string? SummaryJson { get; set; }

    public ICollection<SupervisorReview> Reviews { get; set; } = new List<SupervisorReview>();
}
