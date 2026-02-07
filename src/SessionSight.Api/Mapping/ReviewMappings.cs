using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;

namespace SessionSight.Api.Mapping;

public static class ReviewMappings
{
    public static ReviewQueueItemDto ToQueueItemDto(this ExtractionResult extraction) =>
        new(extraction.Id,
            extraction.SessionId,
            $"{extraction.Session.Patient.FirstName} {extraction.Session.Patient.LastName}",
            extraction.Session.SessionDate,
            extraction.ReviewStatus,
            extraction.OverallConfidence,
            extraction.ReviewReasons,
            extraction.ExtractedAt);

    public static ReviewDetailDto ToDetailDto(this ExtractionResult extraction, List<SupervisorReviewDto> reviews) =>
        new(extraction.Id,
            extraction.SessionId,
            $"{extraction.Session.Patient.FirstName} {extraction.Session.Patient.LastName}",
            extraction.Session.SessionDate,
            extraction.ReviewStatus,
            extraction.OverallConfidence,
            extraction.RequiresReview,
            extraction.ReviewReasons,
            extraction.SummaryJson,
            extraction.Data,
            reviews);

    public static SupervisorReviewDto ToDto(this SupervisorReview review) =>
        new(review.Id,
            review.Action,
            review.ReviewerName,
            review.Notes,
            review.ReviewedAt);
}
