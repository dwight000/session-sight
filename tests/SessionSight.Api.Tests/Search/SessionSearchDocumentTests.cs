using FluentAssertions;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Api.Tests.Search;

public class SessionSearchDocumentTests
{
    [Fact]
    public void SessionSearchDocument_HasDefaultValues()
    {
        var doc = new SessionSearchDocument();

        doc.Id.Should().BeEmpty();
        doc.SessionId.Should().BeEmpty();
        doc.PatientId.Should().BeEmpty();
        doc.SessionDate.Should().Be(default);
        doc.SessionType.Should().BeNull();
        doc.Content.Should().BeNull();
        doc.Summary.Should().BeNull();
        doc.PrimaryDiagnosis.Should().BeNull();
        doc.Interventions.Should().BeNull();
        doc.RiskLevel.Should().BeNull();
        doc.MoodScore.Should().BeNull();
        doc.ContentVector.Should().BeNull();
    }

    [Fact]
    public void SessionSearchDocument_CanBePopulated()
    {
        var sessionDate = DateTimeOffset.UtcNow;
        var vector = new float[] { 0.1f, 0.2f, 0.3f };

        var doc = new SessionSearchDocument
        {
            Id = "doc-123",
            SessionId = "session-456",
            PatientId = "patient-789",
            SessionDate = sessionDate,
            SessionType = "Individual",
            Content = "Session content here",
            Summary = "Brief summary",
            PrimaryDiagnosis = "F32.1 Major Depressive Disorder",
            Interventions = new List<string> { "CBT", "Mindfulness" },
            RiskLevel = "Low",
            MoodScore = 6,
            ContentVector = vector
        };

        doc.Id.Should().Be("doc-123");
        doc.SessionId.Should().Be("session-456");
        doc.PatientId.Should().Be("patient-789");
        doc.SessionDate.Should().Be(sessionDate);
        doc.SessionType.Should().Be("Individual");
        doc.Content.Should().Be("Session content here");
        doc.Summary.Should().Be("Brief summary");
        doc.PrimaryDiagnosis.Should().Be("F32.1 Major Depressive Disorder");
        doc.Interventions.Should().BeEquivalentTo(new[] { "CBT", "Mindfulness" });
        doc.RiskLevel.Should().Be("Low");
        doc.MoodScore.Should().Be(6);
        doc.ContentVector.Should().BeEquivalentTo(vector);
    }
}
