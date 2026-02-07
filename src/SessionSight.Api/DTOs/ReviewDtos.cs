using SessionSight.Core.Enums;

namespace SessionSight.Api.DTOs;

public record ReviewQueueItemDto(
    Guid ExtractionId,
    Guid SessionId,
    string PatientName,
    DateOnly SessionDate,
    ReviewStatus ReviewStatus,
    double OverallConfidence,
    List<string> ReviewReasons,
    DateTime ExtractedAt);

public record ReviewDetailDto(
    Guid ExtractionId,
    Guid SessionId,
    string PatientName,
    DateOnly SessionDate,
    ReviewStatus ReviewStatus,
    double OverallConfidence,
    bool RequiresReview,
    List<string> ReviewReasons,
    string? SummaryJson,
    object Data,
    List<SupervisorReviewDto> Reviews);

public record SubmitReviewRequest(
    [property: System.Text.Json.Serialization.JsonRequired] ReviewStatus Action,
    [property: System.Text.Json.Serialization.JsonRequired] string ReviewerName,
    string? Notes);

public record SupervisorReviewDto(
    Guid Id,
    ReviewStatus Action,
    string ReviewerName,
    string? Notes,
    DateTime ReviewedAt);

public record ReviewStatsDto(int PendingCount, int ApprovedToday, int DismissedToday);
