using System.ComponentModel.DataAnnotations;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public Guid TherapistId { get; set; }
    public Therapist Therapist { get; set; } = null!;
    public DateOnly SessionDate { get; set; }
    public SessionType SessionType { get; set; }
    public SessionModality Modality { get; set; }
    public int? DurationMinutes { get; set; }
    public int SessionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public SessionDocument? Document { get; set; }
    public ExtractionResult? Extraction { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
