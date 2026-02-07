using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SessionSight.Api.Controllers;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;

namespace SessionSight.Api.Tests.Controllers;

public class ReviewControllerTests
{
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly ReviewController _controller;

    public ReviewControllerTests()
    {
        _mockReviewRepo = new Mock<IReviewRepository>();
        _controller = new ReviewController(
            _mockReviewRepo.Object,
            Mock.Of<ILogger<ReviewController>>());
    }

    #region GetReviewQueue Tests

    [Fact]
    public async Task GetReviewQueue_NoFilter_ReturnsAll()
    {
        var extractions = new List<ExtractionResult>
        {
            CreateExtractionWithSession(ReviewStatus.Pending),
            CreateExtractionWithSession(ReviewStatus.Approved)
        };
        _mockReviewRepo.Setup(r => r.GetReviewQueueAsync(null, null, null))
            .ReturnsAsync(extractions);

        var result = await _controller.GetReviewQueue(null, null, null);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = okResult.Value.Should().BeOfType<List<ReviewQueueItemDto>>().Subject;
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetReviewQueue_WithStatusFilter_PassesFilter()
    {
        _mockReviewRepo.Setup(r => r.GetReviewQueueAsync(ReviewStatus.Pending, null, null))
            .ReturnsAsync(new List<ExtractionResult>());

        await _controller.GetReviewQueue(ReviewStatus.Pending, null, null);

        _mockReviewRepo.Verify(r => r.GetReviewQueueAsync(ReviewStatus.Pending, null, null), Times.Once);
    }

    [Fact]
    public async Task GetReviewQueue_WithDateRange_PassesDates()
    {
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 12, 31);
        _mockReviewRepo.Setup(r => r.GetReviewQueueAsync(null, startDate, endDate))
            .ReturnsAsync(new List<ExtractionResult>());

        await _controller.GetReviewQueue(null, startDate, endDate);

        _mockReviewRepo.Verify(r => r.GetReviewQueueAsync(null, startDate, endDate), Times.Once);
    }

    [Fact]
    public async Task GetReviewQueue_ReturnsCorrectDtoShape()
    {
        var extraction = CreateExtractionWithSession(ReviewStatus.Pending);
        extraction.ReviewReasons = new List<string> { "Risk: Low confidence on suicidal ideation" };
        _mockReviewRepo.Setup(r => r.GetReviewQueueAsync(null, null, null))
            .ReturnsAsync(new List<ExtractionResult> { extraction });

        var result = await _controller.GetReviewQueue(null, null, null);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = okResult.Value.Should().BeOfType<List<ReviewQueueItemDto>>().Subject;
        var item = items.First();
        item.ReviewStatus.Should().Be(ReviewStatus.Pending);
        item.ReviewReasons.Should().Contain("Risk: Low confidence on suicidal ideation");
        item.PatientName.Should().Be("Jane Doe");
    }

    #endregion

    #region GetReviewDetail Tests

    [Fact]
    public async Task GetReviewDetail_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        _mockReviewRepo.Setup(r => r.GetExtractionBySessionIdAsync(sessionId))
            .ReturnsAsync((ExtractionResult?)null);

        var result = await _controller.GetReviewDetail(sessionId);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetReviewDetail_Found_ReturnsDetail()
    {
        var sessionId = Guid.NewGuid();
        var extraction = CreateExtractionWithSession(ReviewStatus.Pending);
        extraction.SessionId = sessionId;
        extraction.Reviews = new List<SupervisorReview>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Action = ReviewStatus.Approved,
                ReviewerName = "Dr. Smith",
                Notes = "Looks good",
                ReviewedAt = DateTime.UtcNow
            }
        };
        _mockReviewRepo.Setup(r => r.GetExtractionBySessionIdAsync(sessionId))
            .ReturnsAsync(extraction);

        var result = await _controller.GetReviewDetail(sessionId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = okResult.Value.Should().BeOfType<ReviewDetailDto>().Subject;
        detail.SessionId.Should().Be(sessionId);
        detail.Reviews.Should().HaveCount(1);
        detail.Reviews[0].ReviewerName.Should().Be("Dr. Smith");
    }

    #endregion

    #region SubmitReview Tests

    [Fact]
    public async Task SubmitReview_InvalidAction_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid();
        var request = new SubmitReviewRequest(ReviewStatus.Pending, "Dr. Smith", null);

        var result = await _controller.SubmitReview(sessionId, request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitReview_EmptyReviewerName_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid();
        var request = new SubmitReviewRequest(ReviewStatus.Approved, "  ", null);

        var result = await _controller.SubmitReview(sessionId, request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitReview_SessionNotFound_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var request = new SubmitReviewRequest(ReviewStatus.Approved, "Dr. Smith", "Confirmed");
        _mockReviewRepo.Setup(r => r.GetExtractionBySessionIdAsync(sessionId))
            .ReturnsAsync((ExtractionResult?)null);

        var result = await _controller.SubmitReview(sessionId, request);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SubmitReview_Approved_CreatesReviewAndUpdatesStatus()
    {
        var sessionId = Guid.NewGuid();
        var extraction = CreateExtractionWithSession(ReviewStatus.Pending);
        extraction.SessionId = sessionId;
        var request = new SubmitReviewRequest(ReviewStatus.Approved, "Dr. Smith", "Confirmed safe");

        _mockReviewRepo.Setup(r => r.GetExtractionBySessionIdAsync(sessionId))
            .ReturnsAsync(extraction);
        _mockReviewRepo.Setup(r => r.AddReviewAsync(It.IsAny<SupervisorReview>()))
            .ReturnsAsync((SupervisorReview r) => r);

        var result = await _controller.SubmitReview(sessionId, request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<SupervisorReviewDto>().Subject;
        dto.Action.Should().Be(ReviewStatus.Approved);
        dto.ReviewerName.Should().Be("Dr. Smith");

        _mockReviewRepo.Verify(r => r.UpdateExtractionReviewStatusAsync(extraction.Id, ReviewStatus.Approved), Times.Once);
    }

    [Fact]
    public async Task SubmitReview_Dismissed_CreatesReviewAndUpdatesStatus()
    {
        var sessionId = Guid.NewGuid();
        var extraction = CreateExtractionWithSession(ReviewStatus.Pending);
        extraction.SessionId = sessionId;
        var request = new SubmitReviewRequest(ReviewStatus.Dismissed, "Dr. Smith", "False positive");

        _mockReviewRepo.Setup(r => r.GetExtractionBySessionIdAsync(sessionId))
            .ReturnsAsync(extraction);
        _mockReviewRepo.Setup(r => r.AddReviewAsync(It.IsAny<SupervisorReview>()))
            .ReturnsAsync((SupervisorReview r) => r);

        var result = await _controller.SubmitReview(sessionId, request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<SupervisorReviewDto>().Subject;
        dto.Action.Should().Be(ReviewStatus.Dismissed);

        _mockReviewRepo.Verify(r => r.UpdateExtractionReviewStatusAsync(extraction.Id, ReviewStatus.Dismissed), Times.Once);
    }

    #endregion

    #region GetReviewStats Tests

    [Fact]
    public async Task GetReviewStats_ReturnsStats()
    {
        _mockReviewRepo.Setup(r => r.GetReviewStatsAsync())
            .ReturnsAsync(new ReviewStats(5, 3, 1));

        var result = await _controller.GetReviewStats();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var stats = okResult.Value.Should().BeOfType<ReviewStatsDto>().Subject;
        stats.PendingCount.Should().Be(5);
        stats.ApprovedToday.Should().Be(3);
        stats.DismissedToday.Should().Be(1);
    }

    #endregion

    #region Helpers

    private static ExtractionResult CreateExtractionWithSession(ReviewStatus status)
    {
        return new ExtractionResult
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            SchemaVersion = "1.0.0",
            ModelUsed = "gpt-4o",
            OverallConfidence = 0.85,
            RequiresReview = status == ReviewStatus.Pending,
            ReviewStatus = status,
            ReviewReasons = new List<string>(),
            ExtractedAt = DateTime.UtcNow,
            Data = new ClinicalExtraction(),
            Session = new Session
            {
                Id = Guid.NewGuid(),
                SessionDate = new DateOnly(2024, 6, 15),
                Patient = new Patient
                {
                    Id = Guid.NewGuid(),
                    ExternalId = "P001",
                    FirstName = "Jane",
                    LastName = "Doe"
                }
            },
            Reviews = new List<SupervisorReview>()
        };
    }

    #endregion
}
