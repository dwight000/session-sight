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
    public DateTime ExtractedAt { get; set; }
    public ClinicalExtraction Data { get; set; } = new();
}
