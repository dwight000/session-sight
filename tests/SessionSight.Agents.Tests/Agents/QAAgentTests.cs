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

    #region BuildContextString Tests

    [Fact]
    public void BuildContextString_IncludesInterventions()
    {
        var doc = new SessionSearchDocument
        {
            SessionId = "abc-123",
            SessionDate = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
            SessionType = "Individual",
            Interventions = ["CognitiveRestructuring", "Relaxation"],
            Content = "Ongoing anxiety",
            Summary = "Session summary"
        };
        var results = new List<SearchResult<SessionSearchDocument>>
        {
            SearchModelFactory.SearchResult(doc, 0.9, null)
        };

        var context = QAAgent.BuildContextString(results);

        context.Should().Contain("Interventions: CognitiveRestructuring, Relaxation");
    }

    [Fact]
    public void BuildContextString_OmitsInterventionsWhenEmpty()
    {
        var doc = new SessionSearchDocument
        {
            SessionId = "abc-123",
            SessionDate = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
            Content = "Ongoing anxiety"
        };
        var results = new List<SearchResult<SessionSearchDocument>>
        {
            SearchModelFactory.SearchResult(doc, 0.9, null)
        };

        var context = QAAgent.BuildContextString(results);

        context.Should().NotContain("Interventions:");
    }

    #endregion

    #region ComplexityPrompt Content

    [Fact]
    public void ComplexityPrompt_ClassifiesSingleFieldQueriesAsSimple()
    {
        // The prompt should explicitly list single-field queries as simple examples
        QAPrompts.ComplexityPrompt.Should().Contain("risk level");
        QAPrompts.ComplexityPrompt.Should().Contain("single value");
    }

    [Fact]
    public void ComplexityPrompt_ClassifiesTrendQueriesAsComplex()
    {
        QAPrompts.ComplexityPrompt.Should().Contain("over time");
        QAPrompts.ComplexityPrompt.Should().Contain("trend");
    }

    #endregion

    #region FallbackSourcesFromToolTrace Tests

    [Fact]
    public void FallbackSourcesFromToolTrace_WithSearchSessionsOutput_ExtractsSessionIds()
    {
        var response = new QAResponse();
        var trace = new List<SessionSight.Agents.Tools.ToolCallEntry>
        {
            new("search_sessions", true, OutputJson: """
                {
                    "results": [
                        {"sessionId": "abc-123", "score": 0.9},
                        {"sessionId": "def-456", "score": 0.8}
                    ]
                }
                """)
        };

        QAAgent.FallbackSourcesFromToolTrace(response, trace);

        response.Sources.Should().HaveCount(2);
        response.Sources.Select(s => s.SessionId).Should().Contain("abc-123");
        response.Sources.Select(s => s.SessionId).Should().Contain("def-456");
    }

    [Fact]
    public void FallbackSourcesFromToolTrace_WithGetSessionDetailOutput_ExtractsSessionId()
    {
        var response = new QAResponse();
        var trace = new List<SessionSight.Agents.Tools.ToolCallEntry>
        {
            new("get_session_detail", true, OutputJson: """
                {"sessionId": "abc-123", "data": {"mood": 5}}
                """)
        };

        QAAgent.FallbackSourcesFromToolTrace(response, trace);

        response.Sources.Should().HaveCount(1);
        response.Sources[0].SessionId.Should().Be("abc-123");
    }

    [Fact]
    public void FallbackSourcesFromToolTrace_WithNoRelevantTools_DoesNotAddSources()
    {
        var response = new QAResponse();
        var trace = new List<SessionSight.Agents.Tools.ToolCallEntry>
        {
            new("aggregate_metrics", true, OutputJson: """
                {"metricType": "risk_distribution", "distribution": {"High": 1}}
                """)
        };

        QAAgent.FallbackSourcesFromToolTrace(response, trace);

        response.Sources.Should().BeNullOrEmpty();
    }

    [Fact]
    public void FallbackSourcesFromToolTrace_DeduplicatesSessionIds()
    {
        var response = new QAResponse();
        var trace = new List<SessionSight.Agents.Tools.ToolCallEntry>
        {
            new("search_sessions", true, OutputJson: """
                {"results": [{"sessionId": "abc-123"}, {"sessionId": "abc-123"}]}
                """),
            new("get_session_detail", true, OutputJson: """
                {"sessionId": "abc-123"}
                """)
        };

        QAAgent.FallbackSourcesFromToolTrace(response, trace);

        response.Sources.Should().HaveCount(1);
    }

    [Fact]
    public void FallbackSourcesFromToolTrace_SkipsFailedToolCalls()
    {
        var response = new QAResponse();
        var trace = new List<SessionSight.Agents.Tools.ToolCallEntry>
        {
            new("search_sessions", false, OutputJson: """
                {"results": [{"sessionId": "abc-123"}]}
                """)
        };

        QAAgent.FallbackSourcesFromToolTrace(response, trace);

        response.Sources.Should().BeNullOrEmpty();
    }

    #endregion
}
