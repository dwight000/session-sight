using System.Text.Json.Serialization;
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
    [property: JsonRequired] Guid PatientId,
    [property: JsonRequired] Guid TherapistId,
    [property: JsonRequired] DateOnly SessionDate,
    [property: JsonRequired] SessionType SessionType,
    [property: JsonRequired] SessionModality Modality,
    int? DurationMinutes,
    [property: JsonRequired] int SessionNumber);

public record UpdateSessionRequest(
    [property: JsonRequired] Guid TherapistId,
    [property: JsonRequired] DateOnly SessionDate,
    [property: JsonRequired] SessionType SessionType,
    [property: JsonRequired] SessionModality Modality,
    int? DurationMinutes,
    [property: JsonRequired] int SessionNumber);
