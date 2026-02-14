using SessionSight.Core.Enums;

namespace SessionSight.Api.DTOs;

public record ProcessingJobDto(
    Guid Id,
    string JobKey,
    JobStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);
