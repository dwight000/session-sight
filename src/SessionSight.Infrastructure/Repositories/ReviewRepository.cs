using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly SessionSightDbContext _context;

    public ReviewRepository(SessionSightDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ExtractionResult>> GetReviewQueueAsync(
        ReviewStatus? status, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _context.Extractions
            .Include(e => e.Session)
                .ThenInclude(s => s.Patient)
            .Where(e => e.ReviewStatus != ReviewStatus.NotFlagged);

        if (status.HasValue)
        {
            query = query.Where(e => e.ReviewStatus == status.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(e => e.Session.SessionDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.Session.SessionDate <= endDate.Value);
        }

        return await query
            .OrderByDescending(e => e.ExtractedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<SupervisorReview> AddReviewAsync(SupervisorReview review)
    {
        review.Id = Guid.NewGuid();
        review.ReviewedAt = DateTime.UtcNow;
        _context.SupervisorReviews.Add(review);
        await _context.SaveChangesAsync();
        return review;
    }

    public async Task<IEnumerable<SupervisorReview>> GetReviewsByExtractionIdAsync(Guid extractionId)
        => await _context.SupervisorReviews
            .Where(r => r.ExtractionId == extractionId)
            .OrderByDescending(r => r.ReviewedAt)
            .AsNoTracking()
            .ToListAsync();

    public async Task<ExtractionResult?> GetExtractionBySessionIdAsync(Guid sessionId)
        => await _context.Extractions
            .Include(e => e.Session)
                .ThenInclude(s => s.Patient)
            .Include(e => e.Reviews)
            .FirstOrDefaultAsync(e => e.SessionId == sessionId);

    public async Task UpdateExtractionReviewStatusAsync(Guid extractionId, ReviewStatus status)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId);
        if (extraction is null)
        {
            throw new InvalidOperationException($"Extraction {extractionId} not found");
        }

        extraction.ReviewStatus = status;
        await _context.SaveChangesAsync();
    }

    public async Task<ReviewStats> GetReviewStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var pendingCount = await _context.Extractions
            .CountAsync(e => e.ReviewStatus == ReviewStatus.Pending);

        var approvedToday = await _context.SupervisorReviews
            .CountAsync(r => r.Action == ReviewStatus.Approved && r.ReviewedAt >= today && r.ReviewedAt < tomorrow);

        var dismissedToday = await _context.SupervisorReviews
            .CountAsync(r => r.Action == ReviewStatus.Dismissed && r.ReviewedAt >= today && r.ReviewedAt < tomorrow);

        return new ReviewStats(pendingCount, approvedToday, dismissedToday);
    }
}
