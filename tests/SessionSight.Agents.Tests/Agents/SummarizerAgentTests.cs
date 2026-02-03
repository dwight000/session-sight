using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenAI.Chat;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Core.Schema;
using CoreExtractionResult = SessionSight.Core.Entities.ExtractionResult;

namespace SessionSight.Agents.Tests.Agents;

public class SummarizerAgentTests
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SummarizerAgent> _logger;
    private readonly SummarizerAgent _agent;

    public SummarizerAgentTests()
    {
        _clientFactory = Substitute.For<IAIFoundryClientFactory>();
        _modelRouter = Substitute.For<IModelRouter>();
        _sessionRepository = Substitute.For<ISessionRepository>();
        _logger = Substitute.For<ILogger<SummarizerAgent>>();

        _modelRouter.SelectModel(ModelTask.Summarization).Returns("gpt-4o-mini");

        _agent = new SummarizerAgent(
            _clientFactory,
            _modelRouter,
            _sessionRepository,
            _logger);
    }

    [Fact]
    public void Name_ReturnsSummarizerAgent()
    {
        _agent.Name.Should().Be("SummarizerAgent");
    }

    #region ExtractJson Tests

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
    public void ExtractJson_JsonCodeBlockUppercase_ExtractsJson()
    {
        var input = """
            ```JSON
            {"test": "value"}
            ```
            """;
        var result = SummarizerAgent.ExtractJson(input);
        result.Should().Be("""{"test": "value"}""");
    }

    [Fact]
    public void ExtractJson_WhitespaceAroundJson_TrimsCorrectly()
    {
        var input = "   {\"test\": \"value\"}   ";
        var result = SummarizerAgent.ExtractJson(input);
        result.Should().Be("{\"test\": \"value\"}");
    }

    #endregion

    #region ParseSessionSummary Tests

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
    public void ParseSessionSummary_HighRiskLevel_ReturnsRiskSummary()
    {
        var json = """
            {
                "oneLiner": "Crisis session",
                "riskFlags": {
                    "riskLevel": "High",
                    "flags": ["Active SI", "Plan identified"],
                    "requiresReview": true
                }
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.RiskFlags.Should().NotBeNull();
        result.RiskFlags!.RiskLevel.Should().Be("High");
        result.RiskFlags.RequiresReview.Should().BeTrue();
    }

    [Fact]
    public void ParseSessionSummary_LowRiskWithReview_ReturnsRiskSummary()
    {
        var json = """
            {
                "oneLiner": "Session with concerns",
                "riskFlags": {
                    "riskLevel": "Low",
                    "flags": [],
                    "requiresReview": true
                }
            }
            """;

        var result = SummarizerAgent.ParseSessionSummary(json);

        result.RiskFlags.Should().NotBeNull();
        result.RiskFlags!.RequiresReview.Should().BeTrue();
    }

    #endregion

    #region ParsePatientSummary Tests

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
    public void ParsePatientSummary_CaseInsensitiveMoodTrend_ParsesCorrectly()
    {
        var json = """{"moodTrend": "improving"}""";

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.MoodTrend.Should().Be(MoodTrend.Improving);
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

    [Fact]
    public void ParsePatientSummary_EmptyRecurringThemes_ReturnsEmptyList()
    {
        var json = """
            {
                "progressNarrative": "Test",
                "recurringThemes": []
            }
            """;

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.RecurringThemes.Should().BeEmpty();
    }

    [Fact]
    public void ParsePatientSummary_EmptyEffectiveInterventions_ReturnsEmptyList()
    {
        var json = """
            {
                "progressNarrative": "Test",
                "effectiveInterventions": []
            }
            """;

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.EffectiveInterventions.Should().BeEmpty();
    }

    [Fact]
    public void ParsePatientSummary_MissingFields_UsesDefaults()
    {
        var json = """
            {
                "progressNarrative": "Minimal patient summary"
            }
            """;

        var result = SummarizerAgent.ParsePatientSummary(json);

        result.ProgressNarrative.Should().Be("Minimal patient summary");
        result.MoodTrend.Should().Be(MoodTrend.InsufficientData);
        result.RecurringThemes.Should().BeEmpty();
        result.GoalProgress.Should().BeEmpty();
        result.EffectiveInterventions.Should().BeEmpty();
        result.RecommendedFocus.Should().BeEmpty();
        result.RiskTrendSummary.Should().BeEmpty();
    }

    #endregion

    #region SummarizePatientAsync Tests

    [Fact]
    public async Task SummarizePatientAsync_NoSessions_ReturnsInsufficientDataSummary()
    {
        var patientId = Guid.NewGuid();
        _sessionRepository.GetByPatientIdInDateRangeAsync(patientId, null, null)
            .Returns(new List<Session>());

        var result = await _agent.SummarizePatientAsync(patientId, null, null);

        result.PatientId.Should().Be(patientId);
        result.SessionCount.Should().Be(0);
        result.MoodTrend.Should().Be(MoodTrend.InsufficientData);
        result.ProgressNarrative.Should().Contain("No sessions");
    }

    [Fact]
    public async Task SummarizePatientAsync_NoSessionsWithExtractions_ReturnsInsufficientDataSummary()
    {
        var patientId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            new() { Id = Guid.NewGuid(), PatientId = patientId, SessionDate = DateOnly.FromDateTime(DateTime.Today), Extraction = null }
        };
        _sessionRepository.GetByPatientIdInDateRangeAsync(patientId, null, null)
            .Returns(sessions);

        var result = await _agent.SummarizePatientAsync(patientId, null, null);

        result.SessionCount.Should().Be(0);
        result.MoodTrend.Should().Be(MoodTrend.InsufficientData);
    }

    #endregion

    #region SummarizePracticeAsync Tests

    [Fact]
    public async Task SummarizePracticeAsync_NoSessions_ReturnsEmptySummary()
    {
        var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var end = DateOnly.FromDateTime(DateTime.Today);

        _sessionRepository.GetAllInDateRangeAsync(start, end).Returns(new List<Session>());
        _sessionRepository.GetFlaggedSessionsAsync(start, end).Returns(new List<Session>());

        var result = await _agent.SummarizePracticeAsync(start, end);

        result.TotalSessions.Should().Be(0);
        result.TotalPatients.Should().Be(0);
        result.SessionsRequiringReview.Should().Be(0);
        result.FlaggedPatientCount.Should().Be(0);
        result.Period.Start.Should().Be(start);
        result.Period.End.Should().Be(end);
    }

    [Fact]
    public async Task SummarizePracticeAsync_WithSessions_CalculatesMetrics()
    {
        var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var end = DateOnly.FromDateTime(DateTime.Today);
        var patient1 = Guid.NewGuid();
        var patient2 = Guid.NewGuid();

        var sessions = new List<Session>
        {
            CreateSessionWithExtraction(patient1, DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), RiskLevelOverall.Low),
            CreateSessionWithExtraction(patient1, DateOnly.FromDateTime(DateTime.Today.AddDays(-10)), RiskLevelOverall.Low),
            CreateSessionWithExtraction(patient2, DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), RiskLevelOverall.Moderate)
        };

        _sessionRepository.GetAllInDateRangeAsync(start, end).Returns(sessions);
        _sessionRepository.GetFlaggedSessionsAsync(start, end).Returns(new List<Session>());

        var result = await _agent.SummarizePracticeAsync(start, end);

        result.TotalSessions.Should().Be(3);
        result.TotalPatients.Should().Be(2);
        result.AverageSessionsPerPatient.Should().Be(1.5);
        result.RiskDistribution.Low.Should().Be(2);
        result.RiskDistribution.Moderate.Should().Be(1);
    }

    [Fact]
    public async Task SummarizePracticeAsync_WithFlaggedSessions_CountsFlagged()
    {
        var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var end = DateOnly.FromDateTime(DateTime.Today);
        var patient1 = Guid.NewGuid();

        var allSessions = new List<Session>
        {
            CreateSessionWithExtraction(patient1, DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), RiskLevelOverall.High)
        };

        var flaggedSessions = new List<Session>
        {
            CreateSessionWithExtraction(patient1, DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), RiskLevelOverall.High, requiresReview: true)
        };

        _sessionRepository.GetAllInDateRangeAsync(start, end).Returns(allSessions);
        _sessionRepository.GetFlaggedSessionsAsync(start, end).Returns(flaggedSessions);

        var result = await _agent.SummarizePracticeAsync(start, end);

        result.SessionsRequiringReview.Should().Be(1);
        result.FlaggedPatientCount.Should().Be(1);
    }

    [Fact]
    public async Task SummarizePracticeAsync_WithInterventions_AggregatesTopInterventions()
    {
        var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var end = DateOnly.FromDateTime(DateTime.Today);
        var patient1 = Guid.NewGuid();

        var session = CreateSessionWithExtraction(patient1, DateOnly.FromDateTime(DateTime.Today), RiskLevelOverall.Low);
        session.Extraction!.Data.Interventions.TechniquesUsed = new ExtractedField<List<TechniqueUsed>>
        {
            Value = new List<TechniqueUsed> { TechniqueUsed.Cbt, TechniqueUsed.Mindfulness }
        };

        var sessions = new List<Session> { session };

        _sessionRepository.GetAllInDateRangeAsync(start, end).Returns(sessions);
        _sessionRepository.GetFlaggedSessionsAsync(start, end).Returns(new List<Session>());

        var result = await _agent.SummarizePracticeAsync(start, end);

        result.TopInterventions.Should().HaveCount(2);
        result.TopInterventions.Should().Contain(i => i.Intervention == "Cbt");
        result.TopInterventions.Should().Contain(i => i.Intervention == "Mindfulness");
    }

    [Fact]
    public async Task SummarizePracticeAsync_RiskDistribution_CountsAllLevels()
    {
        var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var end = DateOnly.FromDateTime(DateTime.Today);

        var sessions = new List<Session>
        {
            CreateSessionWithExtraction(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), RiskLevelOverall.Low),
            CreateSessionWithExtraction(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), RiskLevelOverall.Moderate),
            CreateSessionWithExtraction(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), RiskLevelOverall.High),
            CreateSessionWithExtraction(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today.AddDays(-4)), RiskLevelOverall.Imminent)
        };

        _sessionRepository.GetAllInDateRangeAsync(start, end).Returns(sessions);
        _sessionRepository.GetFlaggedSessionsAsync(start, end).Returns(new List<Session>());

        var result = await _agent.SummarizePracticeAsync(start, end);

        result.RiskDistribution.Low.Should().Be(1);
        result.RiskDistribution.Moderate.Should().Be(1);
        result.RiskDistribution.High.Should().Be(1);
        result.RiskDistribution.Imminent.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private static Session CreateSessionWithExtraction(
        Guid patientId,
        DateOnly sessionDate,
        RiskLevelOverall riskLevel,
        bool requiresReview = false)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            SessionDate = sessionDate,
            Patient = new Patient
            {
                Id = patientId,
                ExternalId = $"P{patientId.ToString()[..4]}",
                FirstName = "Test",
                LastName = "Patient"
            },
            Extraction = new CoreExtractionResult
            {
                Id = Guid.NewGuid(),
                RequiresReview = requiresReview,
                Data = new ClinicalExtraction
                {
                    RiskAssessment = new RiskAssessmentExtracted
                    {
                        RiskLevelOverall = new ExtractedField<RiskLevelOverall> { Value = riskLevel },
                        SuicidalIdeation = new ExtractedField<SuicidalIdeation> { Value = SuicidalIdeation.None },
                        SelfHarm = new ExtractedField<SelfHarm> { Value = SelfHarm.None },
                        HomicidalIdeation = new ExtractedField<HomicidalIdeation> { Value = HomicidalIdeation.None }
                    },
                    Interventions = new InterventionsExtracted()
                }
            }
        };
    }

    #endregion
}
