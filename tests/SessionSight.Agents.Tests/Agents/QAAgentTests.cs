using Azure.Search.Documents.Models;
using FluentAssertions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Infrastructure.Search;

namespace SessionSight.Agents.Tests.Agents;

public class QAAgentTests
{
    #region QAPrompts Tests

    [Fact]
    public void SystemPrompt_ContainsExpectedContent()
    {
        QAPrompts.SystemPrompt.Should().Contain("clinical Q&A assistant");
        QAPrompts.SystemPrompt.Should().Contain("confidence");
    }

    [Fact]
    public void GetAnswerPrompt_IncludesQuestionAndContext()
    {
        var result = QAPrompts.GetAnswerPrompt("What is the diagnosis?", "Session context here");
        result.Should().Contain("What is the diagnosis?");
        result.Should().Contain("Session context here");
    }

    [Fact]
    public void ComplexityPrompt_ContainsClassificationInstructions()
    {
        QAPrompts.ComplexityPrompt.Should().Contain("simple");
        QAPrompts.ComplexityPrompt.Should().Contain("complex");
    }

    [Fact]
    public void AgenticSystemPrompt_ContainsToolNames()
    {
        QAPrompts.AgenticSystemPrompt.Should().Contain("search_sessions");
        QAPrompts.AgenticSystemPrompt.Should().Contain("get_session_detail");
        QAPrompts.AgenticSystemPrompt.Should().Contain("get_patient_timeline");
        QAPrompts.AgenticSystemPrompt.Should().Contain("aggregate_metrics");
    }

    [Fact]
    public void GetAgenticUserPrompt_IncludesQuestionAndPatientId()
    {
        var patientId = Guid.NewGuid();
        var result = QAPrompts.GetAgenticUserPrompt("How has mood changed?", patientId);
        result.Should().Contain("How has mood changed?");
        result.Should().Contain(patientId.ToString("D"));
    }

    #endregion

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

    [Fact]
    public void ParseQAResponse_WithStringConfidence_Parses()
    {
        var json = """
            {
                "answer": "The patient showed improvement.",
                "confidence": "0.75"
            }
            """;

        var result = QAAgent.ParseQAResponse(json);

        result.Answer.Should().Contain("improvement");
        result.Confidence.Should().Be(0.75);
    }

    [Fact]
    public void ParseQAResponse_ProseAroundCodeFence_Parses()
    {
        var input = """
            Based on my analysis, here is the answer:
            ```json
            {
                "answer": "Mood improved over sessions.",
                "confidence": 0.8
            }
            ```
            I hope this helps!
            """;

        var result = QAAgent.ParseQAResponse(input);

        result.Answer.Should().Contain("Mood improved");
        result.Confidence.Should().Be(0.8);
    }

    #endregion

    #region MaxContextSessions Constant

    [Fact]
    public void MaxContextSessions_IsExpectedValue()
    {
        QAAgent.MaxContextSessions.Should().Be(10);
    }

    #endregion

    #region QAResponse ToolCallCount

    [Fact]
    public void QAResponse_ToolCallCount_DefaultsToZero()
    {
        var response = new QAResponse();
        response.ToolCallCount.Should().Be(0);
    }

    [Fact]
    public void QAResponse_ToolCallCount_CanBeSet()
    {
        var response = new QAResponse { ToolCallCount = 5 };
        response.ToolCallCount.Should().Be(5);
    }

    #endregion
}
