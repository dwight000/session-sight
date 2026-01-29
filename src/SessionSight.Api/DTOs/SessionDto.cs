using SessionSight.Core.Enums;

namespace SessionSight.Api.DTOs;

public record SessionDto(
    Guid Id,
    Guid PatientId,
    Guid TherapistId,
    DateOnly SessionDate,
    SessionType SessionType,
    SessionModality Modality,
    int? DurationMinutes,
    int SessionNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateSessionRequest(
    Guid PatientId,
    Guid TherapistId,
    DateOnly SessionDate,
    SessionType SessionType,
    SessionModality Modality,
    int? DurationMinutes,
    int SessionNumber);

public record UpdateSessionRequest(
    Guid TherapistId,
    DateOnly SessionDate,
    SessionType SessionType,
    SessionModality Modality,
    int? DurationMinutes,
    int SessionNumber);
