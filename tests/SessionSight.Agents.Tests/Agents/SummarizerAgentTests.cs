using FluentAssertions;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;

namespace SessionSight.Agents.Tests.Agents;

public class SummarizerAgentTests
{
    [Fact]
    public void ExtractJson_PlainJson_ReturnsAsIs()
    {
        var json = """{"oneLiner": "Test session summary"}""";
        var result = SummarizerAgent.ExtractJson(json);
        result.Should().Be(json);
    }

    [Fact]
    public void ExtractJson_MarkdownCodeBlock_ExtractsJson()
    {
        var input = """
            ```json
            {"oneLiner": "Test session summary"}
            ```
            """;
        var result = SummarizerAgent.ExtractJson(input);
        result.Should().Be("""{"oneLiner": "Test session summary"}""");
    }

    [Fact]
    public void ExtractJson_GenericCodeBlock_ExtractsContent()
    {
        var input = """
            ```
            {"oneLiner": "Test session summary"}
            ```
            """;
        var result = SummarizerAgent.ExtractJson(input);
        result.Should().Be("""{"oneLiner": "Test session summary"}""");
    }

    [Fact]
    public void ExtractJson_PlainText_ReturnsAsIs()
    {
        var input = "plain text content";
        var result = SummarizerAgent.ExtractJson(input);
        result.Should().Be("plain text content");
    }

    [Fact]
    public void ParseSessionSummary_ValidJson_ParsesAllFields()
    {
        var json = """
            {
                "oneLiner": "Patient discussed anxiety symptoms and coping strategies.",
                "keyPoints": "- Anxiety levels elevated\n- Used CBT techniques\n- Progress on thought challenging",
                "interventionsUsed": ["CBT", "Mindfulness", "Psychoeducation"],
                "nextSessionFocus": "Continue anxiety management and introduce exposure hierarchy",
                "riskFlags": {
                    "riskLevel": "Low",
                    "flags": [],
                    "requiresReview": false
                }
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.OneLiner.Should().Contain("anxiety");
        result.KeyPoints.Should().Contain("CBT");
        result.InterventionsUsed.Should().HaveCount(3);
        result.InterventionsUsed.Should().Contain("Mindfulness");
        result.NextSessionFocus.Should().Contain("exposure");
        result.RiskFlags.Should().BeNull(); // Low risk with no flags returns null
    }

    [Fact]
    public void ParseSessionSummary_WithRiskFlags_ParsesRiskSummary()
    {
        var json = """
            {
                "oneLiner": "Patient disclosed suicidal ideation.",
                "keyPoints": "- Passive suicidal thoughts reported",
                "interventionsUsed": ["Safety planning", "Crisis resources"],
                "nextSessionFocus": "Follow up on safety plan",
                "riskFlags": {
                    "riskLevel": "Moderate",
                    "flags": ["Passive SI", "Recent stressors"],
                    "requiresReview": true
                }
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.RiskFlags.Should().NotBeNull();
        result.RiskFlags!.RiskLevel.Should().Be("Moderate");
        result.RiskFlags.Flags.Should().HaveCount(2);
        result.RiskFlags.RequiresReview.Should().BeTrue();
    }

    [Fact]
    public void ParseSessionSummary_NullRiskFlags_ReturnsNullRiskFlags()
    {
        var json = """
            {
                "oneLiner": "Routine session",
                "keyPoints": "Standard progress",
                "interventionsUsed": [],
                "nextSessionFocus": "Continue treatment",
                "riskFlags": null
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.RiskFlags.Should().BeNull();
    }

    [Fact]
    public void ParseSessionSummary_MalformedJson_ReturnsErrorSummary()
    {
        var badJson = "not valid json at all";

        var result = SummarizerAgent.ParseSessionSummary(badJson);

        result.OneLiner.Should().Contain("Failed to parse");
        result.KeyPoints.Should().Be(badJson);
    }

    [Fact]
    public void ParseSessionSummary_JsonInCodeBlock_ParsesCorrectly()
    {
        var wrappedJson = """
            ```json
            {
                "oneLiner": "Test summary",
                "keyPoints": "Key points here",
                "interventionsUsed": ["CBT"],
                "nextSessionFocus": "Next steps"
            }
            ```
            """;

        var result = SummarizerAgent.ParseSessionSummary(wrappedJson);

        result.OneLiner.Should().Be("Test summary");
        result.InterventionsUsed.Should().Contain("CBT");
    }

    [Fact]
    public void ParseSessionSummary_MissingFields_UsesDefaults()
    {
        var json = """
            {
                "oneLiner": "Minimal summary"
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.OneLiner.Should().Be("Minimal summary");
        result.KeyPoints.Should().BeEmpty();
        result.InterventionsUsed.Should().BeEmpty();
        result.NextSessionFocus.Should().BeEmpty();
        result.RiskFlags.Should().BeNull();
    }

    [Fact]
    public void ParsePatientSummary_ValidJson_ParsesAllFields()
    {
        var json = """
            {
                "progressNarrative": "Patient has shown steady improvement over 6 sessions.",
                "moodTrend": "Improving",
                "recurringThemes": ["Work stress", "Relationship issues", "Self-esteem"],
                "goalProgress": [
                    {"goal": "Reduce anxiety", "status": "Good progress"},
                    {"goal": "Improve sleep", "status": "Ongoing"}
                ],
                "effectiveInterventions": ["CBT", "Sleep hygiene"],
                "recommendedFocus": "Continue CBT and begin behavioral activation",
                "riskTrendSummary": "No risk concerns throughout treatment period"
            }
            """;

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.ProgressNarrative.Should().Contain("steady improvement");
        result.MoodTrend.Should().Be(MoodTrend.Improving);
        result.RecurringThemes.Should().HaveCount(3);
        result.GoalProgress.Should().HaveCount(2);
        result.GoalProgress[0].Goal.Should().Be("Reduce anxiety");
        result.EffectiveInterventions.Should().Contain("Sleep hygiene");
        result.RecommendedFocus.Should().Contain("behavioral activation");
        result.RiskTrendSummary.Should().Contain("No risk concerns");
    }

    [Fact]
    public void ParsePatientSummary_AllMoodTrends_ParsesCorrectly()
    {
        var trends = new[] { "Improving", "Stable", "Declining", "Variable", "InsufficientData" };
        var expectedEnums = new[]
        {
            MoodTrend.Improving,
            MoodTrend.Stable,
            MoodTrend.Declining,
            MoodTrend.Variable,
            MoodTrend.InsufficientData
        };

        for (int i = 0; i < trends.Length; i++)
        {
            var json = $$"""{"moodTrend": "{{trends[i]}}"}""";
            var result = SummarizerAgent.ParsePatientSummary(json);
            result.MoodTrend.Should().Be(expectedEnums[i]);
        }
    }

    [Fact]
    public void ParsePatientSummary_MalformedJson_ReturnsErrorSummary()
    {
        var badJson = "invalid json";

        var result = SummarizerAgent.ParsePatientSummary(badJson);

        result.ProgressNarrative.Should().Contain("Failed to parse");
        result.MoodTrend.Should().Be(MoodTrend.InsufficientData);
    }

    [Fact]
    public void ParsePatientSummary_EmptyGoalProgress_ReturnsEmptyList()
    {
        var json = """
            {
                "progressNarrative": "Test",
                "goalProgress": []
            }
            """;

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.GoalProgress.Should().BeEmpty();
    }

    [Fact]
    public void ParsePatientSummary_UnknownMoodTrend_DefaultsToInsufficientData()
    {
        var json = """{"moodTrend": "UnknownValue"}""";

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.MoodTrend.Should().Be(MoodTrend.InsufficientData);
    }

    [Fact]
    public void ParseSessionSummary_EmptyInterventions_ReturnsEmptyList()
    {
        var json = """
            {
                "oneLiner": "Session summary",
                "interventionsUsed": []
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.InterventionsUsed.Should().BeEmpty();
    }

    [Fact]
    public void ParsePatientSummary_CaseInsensitiveMoodTrend_ParsesCorrectly()
    {
        var json = """{"moodTrend": "improving"}""";

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.MoodTrend.Should().Be(MoodTrend.Improving);
    }

    [Fact]
    public void ParseSessionSummary_LowRiskWithFlags_ReturnsRiskSummary()
    {
        var json = """
            {
                "oneLiner": "Session summary",
                "riskFlags": {
                    "riskLevel": "Low",
                    "flags": ["Historical self-harm"],
                    "requiresReview": false
                }
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.RiskFlags.Should().NotBeNull();
        result.RiskFlags!.Flags.Should().HaveCount(1);
    }

    [Fact]
    public void ParsePatientSummary_GoalProgressWithMissingFields_UsesDefaults()
    {
        var json = """
            {
                "goalProgress": [
                    {"goal": "Test goal"},
                    {"status": "In progress"}
                ]
            }
            """;

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.GoalProgress.Should().HaveCount(2);
        result.GoalProgress[0].Goal.Should().Be("Test goal");
        result.GoalProgress[0].Status.Should().BeEmpty();
        result.GoalProgress[1].Goal.Should().BeEmpty();
        result.GoalProgress[1].Status.Should().Be("In progress");
    }
}
