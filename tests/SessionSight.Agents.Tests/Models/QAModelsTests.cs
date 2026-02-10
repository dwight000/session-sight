using FluentAssertions;
using SessionSight.Agents.Models;

namespace SessionSight.Agents.Tests.Models;

public class QAModelsTests
{
    [Fact]
    public void SourceCitation_DefaultValues_AreCorrect()
    {
        var citation = new SourceCitation();

        citation.SessionId.Should().BeEmpty();
        citation.SessionDate.Should().Be(default);
        citation.SessionType.Should().BeNull();
        citation.Summary.Should().BeNull();
        citation.RelevanceScore.Should().Be(0);
    }

    [Fact]
    public void QAResponse_DefaultValues_AreCorrect()
    {
        var response = new QAResponse();

        response.Question.Should().BeEmpty();
        response.Answer.Should().BeEmpty();
        response.Sources.Should().BeEmpty();
        response.Confidence.Should().Be(0);
        response.ModelUsed.Should().BeEmpty();
        response.Warning.Should().BeNull();
        response.ToolCallCount.Should().Be(0);
        response.GeneratedAt.Should().Be(default);
    }

    [Fact]
    public void SourceCitation_CanSetAllProperties()
    {
        var date = DateTimeOffset.UtcNow;
        var citation = new SourceCitation
        {
            SessionId = "abc-123",
            SessionDate = date,
            SessionType = "Individual",
            Summary = "Patient discussed anxiety",
            RelevanceScore = 0.95
        };

        citation.SessionId.Should().Be("abc-123");
        citation.SessionDate.Should().Be(date);
        citation.SessionType.Should().Be("Individual");
        citation.Summary.Should().Be("Patient discussed anxiety");
        citation.RelevanceScore.Should().Be(0.95);
    }
}
