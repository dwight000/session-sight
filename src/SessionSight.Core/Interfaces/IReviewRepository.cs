using SessionSight.Core.Entities;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Interfaces;

public interface IReviewRepository
{
    Task<IEnumerable<ExtractionResult>> GetReviewQueueAsync(ReviewStatus? status, DateOnly? startDate, DateOnly? endDate);
    Task<SupervisorReview> AddReviewAsync(SupervisorReview review);
    Task<IEnumerable<SupervisorReview>> GetReviewsByExtractionIdAsync(Guid extractionId);
    Task<ExtractionResult?> GetExtractionBySessionIdAsync(Guid sessionId);
    Task UpdateExtractionReviewStatusAsync(Guid extractionId, ReviewStatus status);
    Task<ReviewStats> GetReviewStatsAsync();
}

public record ReviewStats(int PendingCount, int ApprovedToday, int DismissedToday);
