namespace SessionSight.Core.Schema;

public class ExtractionMetadata
{
    public DateTime ExtractionTimestamp { get; set; }
    public string ExtractionModel { get; set; } = "";
    public string ExtractionVersion { get; set; } = "1.0.0";
    public double OverallConfidence { get; set; }
    public List<string> LowConfidenceFields { get; set; } = new();
    public string? ExtractionNotes { get; set; }
    public bool RequiresReview { get; set; }
}
