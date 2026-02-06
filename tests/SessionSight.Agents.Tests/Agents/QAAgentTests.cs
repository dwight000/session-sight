using Azure.Search.Documents.Models;
using FluentAssertions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Agents.Tests.Agents;

public class QAAgentTests
{
    #region ParseQAResponse Tests

    [Fact]
    public void ParseQAResponse_ValidJson_ParsesAllFields()
    {
        var json = """
            {
                "answer": "The patient showed improvement in anxiety levels over the last 3 sessions.",
                "confidence": 0.85,
                "citedSessionIds": ["abc-123", "def-456"]
            }
            """;

        var result = QAAgent.ParseQAResponse(json);

        result.Answer.Should().Contain("improvement in anxiety");
        result.Confidence.Should().Be(0.85);
    }

    [Fact]
    public void ParseQAResponse_MalformedJson_ReturnsErrorResponse()
    {
        var badJson = "this is not valid json at all";

        var result = QAAgent.ParseQAResponse(badJson);

        result.Answer.Should().Contain("Failed to parse");
        result.Confidence.Should().Be(0);
    }

    [Fact]
    public void ParseQAResponse_JsonInCodeBlock_ParsesCorrectly()
    {
        var wrappedJson = """
            ```json
            {
                "answer": "The patient's mood has been stable.",
                "confidence": 0.9,
                "citedSessionIds": ["abc-123"]
            }
            ```
            """;

        var result = QAAgent.ParseQAResponse(wrappedJson);

        result.Answer.Should().Contain("mood has been stable");
        result.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void ParseQAResponse_MissingFields_UsesDefaults()
    {
        var json = """
            {
                "answer": "Minimal answer"
            }
            """;

        var result = QAAgent.ParseQAResponse(json);

        result.Answer.Should().Be("Minimal answer");
        result.Confidence.Should().Be(0);
    }

    [Fact]
    public void ParseQAResponse_ConfidenceAboveOne_ClampedToOne()
    {
        var json = """
            {
                "answer": "Very confident answer",
                "confidence": 1.5
            }
            """;

        var result = QAAgent.ParseQAResponse(json);

        result.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void ParseQAResponse_ConfidenceBelowZero_ClampedToZero()
    {
        var json = """
            {
                "answer": "Negative confidence answer",
                "confidence": -0.5
            }
            """;

        var result = QAAgent.ParseQAResponse(json);

        result.Confidence.Should().Be(0);
    }

    [Fact]
    public void ParseQAResponse_EmptyAnswer_ReturnsEmptyString()
    {
        var json = """
            {
                "answer": "",
                "confidence": 0.5
            }
            """;

        var result = QAAgent.ParseQAResponse(json);

        result.Answer.Should().BeEmpty();
    }

    [Fact]
    public void ParseQAResponse_NullAnswer_ReturnsEmptyString()
    {
        var json = """
            {
                "answer": null,
                "confidence": 0.5
            }
            """;

        var result = QAAgent.ParseQAResponse(json);

        result.Answer.Should().BeEmpty();
    }

    #endregion

    #region MaxContextSessions Constant

    [Fact]
    public void MaxContextSessions_IsExpectedValue()
    {
        QAAgent.MaxContextSessions.Should().Be(10);
    }

    #endregion
}
