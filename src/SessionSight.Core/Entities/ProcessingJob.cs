using SessionSight.Core.Enums;

namespace SessionSight.Core.Entities;

public class ProcessingJob
{
    public Guid Id { get; set; }
    public string JobKey { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
