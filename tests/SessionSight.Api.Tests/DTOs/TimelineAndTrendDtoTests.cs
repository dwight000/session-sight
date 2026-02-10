using FluentAssertions;
using SessionSight.Api.DTOs;
using SessionSight.Core.Enums;

namespace SessionSight.Api.Tests.DTOs;

public class TimelineAndTrendDtoTests
{
    [Fact]
    public void PatientRiskTrendPointDto_AllProperties_Accessible()
    {
        var dto = new PatientRiskTrendPointDto(
            Guid.NewGuid(), new DateOnly(2025, 3, 1), 5,
            "Moderate", 3, 6, true);

        dto.SessionDate.Should().Be(new DateOnly(2025, 3, 1));
        dto.SessionNumber.Should().Be(5);
        dto.RiskLevel.Should().Be("Moderate");
        dto.RiskScore.Should().Be(3);
        dto.MoodScore.Should().Be(6);
        dto.RequiresReview.Should().BeTrue();
    }

    [Fact]
    public void PatientRiskTrendPointDto_NullableProperties_CanBeNull()
    {
        var dto = new PatientRiskTrendPointDto(
            Guid.NewGuid(), new DateOnly(2025, 3, 1), 1,
            null, null, null, false);

        dto.RiskLevel.Should().BeNull();
        dto.RiskScore.Should().BeNull();
        dto.MoodScore.Should().BeNull();
    }

    [Fact]
    public void PatientTimelineEntryDto_AllProperties_Accessible()
    {
        var dto = new PatientTimelineEntryDto(
            Guid.NewGuid(), new DateOnly(2025, 4, 1), 3,
            "Individual", "InPerson", true,
            DocumentStatus.Completed, "note.pdf", "https://blob/note.pdf",
            "Low", 1, 7, false, ReviewStatus.NotFlagged,
            14, "Stable", 1, "Improving");

        dto.SessionDate.Should().Be(new DateOnly(2025, 4, 1));
        dto.SessionNumber.Should().Be(3);
        dto.SessionType.Should().Be("Individual");
        dto.Modality.Should().Be("InPerson");
        dto.HasDocument.Should().BeTrue();
        dto.DocumentStatus.Should().Be(DocumentStatus.Completed);
        dto.DocumentFileName.Should().Be("note.pdf");
        dto.DocumentBlobUri.Should().Be("https://blob/note.pdf");
        dto.RiskLevel.Should().Be("Low");
        dto.RiskScore.Should().Be(1);
        dto.MoodScore.Should().Be(7);
        dto.RequiresReview.Should().BeFalse();
        dto.ReviewStatus.Should().Be(ReviewStatus.NotFlagged);
        dto.DaysSincePreviousSession.Should().Be(14);
        dto.RiskChange.Should().Be("Stable");
        dto.MoodDelta.Should().Be(1);
        dto.MoodChange.Should().Be("Improving");
    }

    [Fact]
    public void PatientTimelineDto_AllProperties_Accessible()
    {
        var entries = new List<PatientTimelineEntryDto>();
        var dto = new PatientTimelineDto(
            Guid.NewGuid(), new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 1),
            10, entries, "Low", false);

        dto.StartDate.Should().Be(new DateOnly(2025, 1, 1));
        dto.EndDate.Should().Be(new DateOnly(2025, 6, 1));
        dto.TotalSessions.Should().Be(10);
        dto.Entries.Should().BeEmpty();
        dto.LatestRiskLevel.Should().Be("Low");
        dto.HasEscalation.Should().BeFalse();
    }

    [Fact]
    public void PatientRiskTrendDto_AllProperties_Accessible()
    {
        var points = new List<PatientRiskTrendPointDto>();
        var period = new RiskTrendPeriodDto(new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 1));
        var dto = new PatientRiskTrendDto(
            Guid.NewGuid(), period, 8, points, "High", true);

        dto.Period.Start.Should().Be(new DateOnly(2025, 1, 1));
        dto.Period.End.Should().Be(new DateOnly(2025, 6, 1));
        dto.TotalSessions.Should().Be(8);
        dto.Points.Should().BeEmpty();
        dto.LatestRiskLevel.Should().Be("High");
        dto.HasEscalation.Should().BeTrue();
    }
}
