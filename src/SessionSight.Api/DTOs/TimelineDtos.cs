using SessionSight.Core.Enums;

namespace SessionSight.Api.DTOs;

public record PatientTimelineDto(
    Guid PatientId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int TotalSessions,
    List<PatientTimelineEntryDto> Entries,
    string? LatestRiskLevel,
    bool HasEscalation);

public record PatientTimelineEntryDto(
    Guid SessionId,
    DateOnly SessionDate,
    int SessionNumber,
    string SessionType,
    string Modality,
    bool HasDocument,
    DocumentStatus? DocumentStatus,
    string? DocumentFileName,
    string? DocumentBlobUri,
    string? RiskLevel,
    int? RiskScore,
    int? MoodScore,
    bool RequiresReview,
    ReviewStatus ReviewStatus,
    int? DaysSincePreviousSession,
    string? RiskChange,
    int? MoodDelta,
    string? MoodChange);
