namespace SessionSight.Api.DTOs;

public record PatientRiskTrendDto(
    Guid PatientId,
    RiskTrendPeriodDto Period,
    int TotalSessions,
    List<PatientRiskTrendPointDto> Points,
    string? LatestRiskLevel,
    bool HasEscalation);

public record RiskTrendPeriodDto(
    DateOnly Start,
    DateOnly End);

public record PatientRiskTrendPointDto(
    Guid SessionId,
    DateOnly SessionDate,
    int SessionNumber,
    string? RiskLevel,
    int? RiskScore,
    int? MoodScore,
    bool RequiresReview);
