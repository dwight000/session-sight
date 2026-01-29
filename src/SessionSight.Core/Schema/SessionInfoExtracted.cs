using SessionSight.Core.Enums;

namespace SessionSight.Core.Schema;

public class SessionInfoExtracted
{
    public ExtractedField<string> PatientId { get; set; } = new();
    public ExtractedField<DateOnly> SessionDate { get; set; } = new();
    public ExtractedField<TimeOnly> SessionStartTime { get; set; } = new();
    public ExtractedField<TimeOnly> SessionEndTime { get; set; } = new();
    public ExtractedField<int> SessionDurationMinutes { get; set; } = new();
    public ExtractedField<SessionType> SessionType { get; set; } = new();
    public ExtractedField<int> SessionNumber { get; set; } = new();
    public ExtractedField<SessionModality> SessionModality { get; set; } = new();
    public ExtractedField<string> TherapistId { get; set; } = new();
}
