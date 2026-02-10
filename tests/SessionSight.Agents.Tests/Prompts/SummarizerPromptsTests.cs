using FluentAssertions;
using SessionSight.Agents.Prompts;

namespace SessionSight.Agents.Tests.Prompts;

public class SummarizerPromptsTests
{
    [Fact]
    public void SystemPrompt_IsNotEmpty()
    {
        SummarizerPrompts.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        SummarizerPrompts.SystemPrompt.Should().Contain("clinical documentation specialist");
    }

    [Fact]
    public void GetSessionSummaryPrompt_ContainsExtractionData()
    {
        var prompt = SummarizerPrompts.GetSessionSummaryPrompt("{\"test\": true}");

        prompt.Should().Contain("{\"test\": true}");
        prompt.Should().Contain("oneLiner");
        prompt.Should().Contain("keyPoints");
        prompt.Should().Contain("interventionsUsed");
        prompt.Should().Contain("nextSessionFocus");
        prompt.Should().Contain("riskFlags");
    }

    [Fact]
    public void GetPatientSummaryPrompt_ContainsSessionCountAndData()
    {
        var prompt = SummarizerPrompts.GetPatientSummaryPrompt("{\"sessions\": []}", 5);

        prompt.Should().Contain("{\"sessions\": []}");
        prompt.Should().Contain("5");
        prompt.Should().Contain("progressNarrative");
        prompt.Should().Contain("moodTrend");
        prompt.Should().Contain("recurringThemes");
        prompt.Should().Contain("goalProgress");
        prompt.Should().Contain("effectiveInterventions");
        prompt.Should().Contain("recommendedFocus");
        prompt.Should().Contain("riskTrendSummary");
    }

    [Fact]
    public void GetPracticeSummaryPrompt_ContainsMetricsData()
    {
        var prompt = SummarizerPrompts.GetPracticeSummaryPrompt("{\"metrics\": {}}");

        prompt.Should().Contain("{\"metrics\": {}}");
        prompt.Should().Contain("observations");
    }
}
