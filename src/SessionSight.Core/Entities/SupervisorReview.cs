using SessionSight.Core.Enums;

namespace SessionSight.Core.Entities;

public class SupervisorReview
{
    public Guid Id { get; set; }
    public Guid ExtractionId { get; set; }
    public ExtractionResult Extraction { get; set; } = null!;
    public ReviewStatus Action { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ReviewedAt { get; set; }
}
