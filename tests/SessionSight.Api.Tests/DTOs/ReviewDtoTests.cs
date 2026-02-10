using FluentAssertions;
using SessionSight.Api.DTOs;
using SessionSight.Core.Enums;

namespace SessionSight.Api.Tests.DTOs;

public class ReviewDtoTests
{
    [Fact]
    public void ReviewDetailDto_CanBeCreated()
    {
        var reviews = new List<SupervisorReviewDto>
        {
            new(Guid.NewGuid(), ReviewStatus.Approved, "Dr. Smith", "Looks good", DateTime.UtcNow)
        };

        var dto = new ReviewDetailDto(
            Guid.NewGuid(), Guid.NewGuid(), "John Doe",
            new DateOnly(2025, 1, 15), ReviewStatus.Pending,
            0.85, true, ["Risk: elevated SI"],
            """{"oneLiner": "test"}""", new { }, reviews);

        dto.ExtractionId.Should().NotBeEmpty();
        dto.SessionId.Should().NotBeEmpty();
        dto.PatientName.Should().Be("John Doe");
        dto.SessionDate.Should().Be(new DateOnly(2025, 1, 15));
        dto.ReviewStatus.Should().Be(ReviewStatus.Pending);
        dto.OverallConfidence.Should().Be(0.85);
        dto.RequiresReview.Should().BeTrue();
        dto.ReviewReasons.Should().ContainSingle();
        dto.SummaryJson.Should().Contain("oneLiner");
        dto.Data.Should().NotBeNull();
        dto.Reviews.Should().HaveCount(1);
    }

    [Fact]
    public void ReviewQueueItemDto_CanBeCreated()
    {
        var extractedAt = DateTime.UtcNow;
        var dto = new ReviewQueueItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Jane Doe",
            new DateOnly(2025, 2, 1), ReviewStatus.Pending,
            0.72, ["Low confidence extraction"], extractedAt);

        dto.ExtractionId.Should().NotBeEmpty();
        dto.SessionId.Should().NotBeEmpty();
        dto.PatientName.Should().Be("Jane Doe");
        dto.SessionDate.Should().Be(new DateOnly(2025, 2, 1));
        dto.ReviewStatus.Should().Be(ReviewStatus.Pending);
        dto.OverallConfidence.Should().Be(0.72);
        dto.ReviewReasons.Should().ContainSingle();
        dto.ExtractedAt.Should().Be(extractedAt);
    }

    [Fact]
    public void SupervisorReviewDto_CanBeCreated()
    {
        var reviewedAt = DateTime.UtcNow;
        var dto = new SupervisorReviewDto(
            Guid.NewGuid(), ReviewStatus.Approved, "Dr. Smith", "All clear", reviewedAt);

        dto.Id.Should().NotBeEmpty();
        dto.Action.Should().Be(ReviewStatus.Approved);
        dto.ReviewerName.Should().Be("Dr. Smith");
        dto.Notes.Should().Be("All clear");
        dto.ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void ReviewStatsDto_CanBeCreated()
    {
        var dto = new ReviewStatsDto(5, 3, 1);

        dto.PendingCount.Should().Be(5);
        dto.ApprovedToday.Should().Be(3);
        dto.DismissedToday.Should().Be(1);
    }

    [Fact]
    public void SubmitReviewRequest_CanBeCreated()
    {
        var dto = new SubmitReviewRequest(ReviewStatus.Approved, "Dr. Smith", "Reviewed and approved");

        dto.Action.Should().Be(ReviewStatus.Approved);
        dto.ReviewerName.Should().Be("Dr. Smith");
        dto.Notes.Should().Be("Reviewed and approved");
    }
}
