using Microsoft.AspNetCore.Mvc;
using SessionSight.Api.DTOs;
using SessionSight.Api.Mapping;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;

namespace SessionSight.Api.Controllers;

[ApiController]
[Route("api/review")]
public partial class ReviewController : ControllerBase
{
    private readonly IReviewRepository _reviewRepository;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IReviewRepository reviewRepository, ILogger<ReviewController> logger)
    {
        _reviewRepository = reviewRepository;
        _logger = logger;
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(List<ReviewQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReviewQueueItemDto>>> GetReviewQueue(
        [FromQuery] ReviewStatus? status,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate)
    {
        LogFetchingQueue(_logger, status);

        var extractions = await _reviewRepository.GetReviewQueueAsync(status, startDate, endDate);
        var items = extractions.Select(e => e.ToQueueItemDto()).ToList();
        return Ok(items);
    }

    [HttpGet("session/{sessionId:guid}")]
    [ProducesResponseType(typeof(ReviewDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDetailDto>> GetReviewDetail(Guid sessionId)
    {
        var extraction = await _reviewRepository.GetExtractionBySessionIdAsync(sessionId);
        if (extraction is null)
        {
            return NotFound($"No extraction found for session {sessionId}");
        }

        var reviewDtos = extraction.Reviews.Select(r => r.ToDto()).ToList();
        return Ok(extraction.ToDetailDto(reviewDtos));
    }

    [HttpPost("session/{sessionId:guid}")]
    [ProducesResponseType(typeof(SupervisorReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupervisorReviewDto>> SubmitReview(
        Guid sessionId,
        [FromBody] SubmitReviewRequest request)
    {
        if (request.Action is not (ReviewStatus.Approved or ReviewStatus.Dismissed))
        {
            return BadRequest("Action must be Approved or Dismissed");
        }

        if (string.IsNullOrWhiteSpace(request.ReviewerName))
        {
            return BadRequest("ReviewerName is required");
        }

        var extraction = await _reviewRepository.GetExtractionBySessionIdAsync(sessionId);
        if (extraction is null)
        {
            return NotFound($"No extraction found for session {sessionId}");
        }

        LogSubmittingReview(_logger, sessionId, request.Action, request.ReviewerName);

        var review = new SupervisorReview
        {
            ExtractionId = extraction.Id,
            Action = request.Action,
            ReviewerName = request.ReviewerName,
            Notes = request.Notes
        };

        var saved = await _reviewRepository.AddReviewAsync(review);
        await _reviewRepository.UpdateExtractionReviewStatusAsync(extraction.Id, request.Action);

        return Ok(saved.ToDto());
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ReviewStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewStatsDto>> GetReviewStats()
    {
        var stats = await _reviewRepository.GetReviewStatsAsync();
        return Ok(new ReviewStatsDto(stats.PendingCount, stats.ApprovedToday, stats.DismissedToday));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching review queue with status filter: {Status}")]
    private static partial void LogFetchingQueue(ILogger logger, ReviewStatus? status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Submitting review for session {SessionId}: {Action} by {ReviewerName}")]
    private static partial void LogSubmittingReview(ILogger logger, Guid sessionId, ReviewStatus action, string reviewerName);
}
