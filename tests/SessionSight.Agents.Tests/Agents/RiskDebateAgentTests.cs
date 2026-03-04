using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SessionSight.Agents.Agents;
using SessionSight.Agents.Models;
using SessionSight.Agents.Orchestration;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Tests.Agents;

public class RiskDebateAgentTests
{
    // --- ShouldTriggerDebate tests ---

    [Fact]
    public void ShouldTriggerDebate_Off_ReturnsFalse()
    {
        var options = new RiskDebateOptions { Enabled = true, TriggerMode = RiskDebateTriggerMode.Off };
        var risk = CreateRiskResult(confidence: 0.5, requiresReview: true);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerDebate_Disabled_ReturnsFalse()
    {
        var options = new RiskDebateOptions { Enabled = false, TriggerMode = RiskDebateTriggerMode.Always };
        var risk = CreateRiskResult(confidence: 0.5);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerDebate_Always_ReturnsTrue()
    {
        var options = new RiskDebateOptions { Enabled = true, TriggerMode = RiskDebateTriggerMode.Always };
        var risk = CreateRiskResult(confidence: 0.95);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerDebate_Flagged_WhenRequiresReview_ReturnsTrue()
    {
        var options = new RiskDebateOptions { Enabled = true, TriggerMode = RiskDebateTriggerMode.Flagged };
        var risk = CreateRiskResult(confidence: 0.9, requiresReview: true);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerDebate_Flagged_NoReview_ReturnsFalse()
    {
        var options = new RiskDebateOptions { Enabled = true, TriggerMode = RiskDebateTriggerMode.Flagged };
        var risk = CreateRiskResult(confidence: 0.9, requiresReview: false);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerDebate_Borderline_WithinThreshold_ReturnsTrue()
    {
        var options = new RiskDebateOptions
        {
            Enabled = true,
            TriggerMode = RiskDebateTriggerMode.Borderline,
            LowConfidenceThreshold = 0.3,
            HighConfidenceThreshold = 0.7
        };
        var risk = CreateRiskResult(confidence: 0.5);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerDebate_Borderline_OutsideThreshold_ReturnsFalse()
    {
        var options = new RiskDebateOptions
        {
            Enabled = true,
            TriggerMode = RiskDebateTriggerMode.Borderline,
            LowConfidenceThreshold = 0.3,
            HighConfidenceThreshold = 0.7
        };
        var risk = CreateRiskResult(confidence: 0.9);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerDebate_Borderline_AtLowBoundary_ReturnsTrue()
    {
        var options = new RiskDebateOptions
        {
            Enabled = true,
            TriggerMode = RiskDebateTriggerMode.Borderline,
            LowConfidenceThreshold = 0.3,
            HighConfidenceThreshold = 0.7
        };
        var risk = CreateRiskResult(confidence: 0.3);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerDebate_Borderline_AtHighBoundary_ReturnsTrue()
    {
        var options = new RiskDebateOptions
        {
            Enabled = true,
            TriggerMode = RiskDebateTriggerMode.Borderline,
            LowConfidenceThreshold = 0.3,
            HighConfidenceThreshold = 0.7
        };
        var risk = CreateRiskResult(confidence: 0.7);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerDebate_Borderline_BelowLow_ReturnsFalse()
    {
        var options = new RiskDebateOptions
        {
            Enabled = true,
            TriggerMode = RiskDebateTriggerMode.Borderline,
            LowConfidenceThreshold = 0.3,
            HighConfidenceThreshold = 0.7
        };
        var risk = CreateRiskResult(confidence: 0.2);

        ExtractionOrchestrator.ShouldTriggerDebate(risk, options).Should().BeFalse();
    }

    // --- ParseJudgeVerdict tests ---

    [Fact]
    public void ParseJudgeVerdict_ValidJson_ReturnsCorrectVerdict()
    {
        var json = """
        {
            "finalRiskLevel": "High",
            "finalConfidence": 0.85,
            "requiresReview": true,
            "reviewReasons": ["Suicidal ideation indicators present"],
            "synthesis": "Evidence supports elevated risk."
        }
        """;

        var verdict = RiskDebateAgent.ParseJudgeVerdict(json);

        verdict.FinalRiskLevel.Should().Be(RiskLevelOverall.High);
        verdict.FinalConfidence.Should().Be(0.85);
        verdict.RequiresReview.Should().BeTrue();
        verdict.ReviewReasons.Should().ContainSingle("Suicidal ideation indicators present");
        verdict.Synthesis.Should().Be("Evidence supports elevated risk.");
    }

    [Fact]
    public void ParseJudgeVerdict_InvalidJson_ReturnsFallback()
    {
        var json = "not valid json at all";

        var verdict = RiskDebateAgent.ParseJudgeVerdict(json);

        verdict.FinalRiskLevel.Should().Be(RiskLevelOverall.Moderate);
        verdict.FinalConfidence.Should().Be(0.3);
        verdict.RequiresReview.Should().BeTrue();
        verdict.ReviewReasons.Should().Contain("Judge response was not valid JSON");
    }

    [Fact]
    public void ParseJudgeVerdict_MissingFields_UsesDefaults()
    {
        var json = "{}";

        var verdict = RiskDebateAgent.ParseJudgeVerdict(json);

        verdict.FinalRiskLevel.Should().Be(RiskLevelOverall.Moderate);
        verdict.FinalConfidence.Should().Be(0.5);
        verdict.RequiresReview.Should().BeFalse();
        verdict.ReviewReasons.Should().BeEmpty();
        verdict.Synthesis.Should().BeEmpty();
    }

    [Fact]
    public void ParseJudgeVerdict_CaseInsensitiveRiskLevel()
    {
        var json = """{"finalRiskLevel": "imminent", "finalConfidence": 0.95}""";

        var verdict = RiskDebateAgent.ParseJudgeVerdict(json);

        verdict.FinalRiskLevel.Should().Be(RiskLevelOverall.Imminent);
    }

    [Fact]
    public void ParseJudgeVerdict_EmptyReviewReasons_ReturnsEmptyList()
    {
        var json = """{"finalRiskLevel": "Low", "finalConfidence": 0.9, "reviewReasons": []}""";

        var verdict = RiskDebateAgent.ParseJudgeVerdict(json);

        verdict.ReviewReasons.Should().BeEmpty();
    }

    // --- RiskDebateOptions defaults ---

    [Fact]
    public void RiskDebateOptions_Defaults_AreCorrect()
    {
        var options = new RiskDebateOptions();

        options.Enabled.Should().BeFalse();
        options.TriggerMode.Should().Be(RiskDebateTriggerMode.Borderline);
        options.LowConfidenceThreshold.Should().Be(0.3);
        options.HighConfidenceThreshold.Should().Be(0.7);
        options.MaxRounds.Should().Be(2);
        options.AdvocateModelOverride.Should().BeNull();
        options.ChallengerModelOverride.Should().BeNull();
        options.JudgeModelOverride.Should().BeNull();
    }

    // --- RiskDebateResult defaults ---

    [Fact]
    public void RiskDebateResult_Defaults_AreCorrect()
    {
        var result = new RiskDebateResult();

        result.Rounds.Should().BeEmpty();
        result.JudgeSynthesis.Should().BeEmpty();
        result.RequiresReview.Should().BeFalse();
        result.ReviewReasons.Should().BeEmpty();
        result.LlmTraces.Should().BeEmpty();
        result.InputTokens.Should().Be(0);
        result.OutputTokens.Should().Be(0);
        result.TotalTokens.Should().Be(0);
    }

    // --- DebateAsync integration tests (mocked IChatClient) ---

    [Fact]
    public async Task DebateAsync_HappyPath_ReturnsDebateResult()
    {
        var (agent, _) = CreateDebateAgent(judgeJson: """
            {"finalRiskLevel":"High","finalConfidence":0.85,"requiresReview":true,"reviewReasons":["Elevated risk"],"synthesis":"Risk elevated."}
            """);
        var riskResult = CreateRiskResult(confidence: 0.5);

        var result = await agent.DebateAsync(riskResult, "Patient reports feeling hopeless.");

        result.FinalRiskLevel.Should().Be(RiskLevelOverall.High);
        result.FinalConfidence.Should().Be(0.85);
        result.RequiresReview.Should().BeTrue();
        result.ReviewReasons.Should().Contain("Elevated risk");
        result.JudgeSynthesis.Should().Be("Risk elevated.");
        result.Rounds.Should().HaveCount(2);
        result.Rounds[0].RoundNumber.Should().Be(1);
        result.Rounds[1].RoundNumber.Should().Be(2);
        result.LlmTraces.Should().HaveCount(5);
        result.AdvocateModel.Should().Be("gpt-4.1-nano");
        result.ChallengerModel.Should().Be("Mistral-Large-3");
        result.JudgeModel.Should().Be("gpt-4.1-mini");
    }

    [Fact]
    public async Task DebateAsync_JudgeReturnsHigherRisk_OverridesRiskLevel()
    {
        var (agent, _) = CreateDebateAgent(judgeJson: """
            {"finalRiskLevel":"Imminent","finalConfidence":0.95,"requiresReview":true,"reviewReasons":["Immediate danger"],"synthesis":"Imminent risk."}
            """);
        var riskResult = CreateRiskResult(confidence: 0.5);

        var result = await agent.DebateAsync(riskResult, "Test note");

        result.FinalRiskLevel.Should().Be(RiskLevelOverall.Imminent);
        result.FinalConfidence.Should().Be(0.95);
    }

    [Fact]
    public async Task DebateAsync_AccumulatesTokens()
    {
        var (agent, _) = CreateDebateAgent(judgeJson: """{"finalRiskLevel":"Low","finalConfidence":0.9}""");
        var riskResult = CreateRiskResult(confidence: 0.5);

        var result = await agent.DebateAsync(riskResult, "Test note");

        // Each call returns 10/5/15 tokens, 5 calls total
        result.InputTokens.Should().Be(50);
        result.OutputTokens.Should().Be(25);
        result.TotalTokens.Should().Be(75);
    }

    [Fact]
    public async Task DebateAsync_ContentFilterOnAdvocate_HandlesGracefully()
    {
        var (agent, clients) = CreateDebateAgent(
            judgeJson: """{"finalRiskLevel":"Moderate","finalConfidence":0.5,"requiresReview":true,"reviewReasons":["Advocate blocked"],"synthesis":"Partial."}""",
            advocateBlocked: true);
        var riskResult = CreateRiskResult(confidence: 0.5);

        var result = await agent.DebateAsync(riskResult, "Test note");

        result.Rounds[0].AdvocateArgument.Should().Be("[Content filter blocked]");
        result.ReviewReasons.Should().Contain("Advocate response blocked by content filter");
    }

    [Fact]
    public async Task DebateAsync_ContentFilterOnChallenger_HandlesGracefully()
    {
        var (agent, clients) = CreateDebateAgent(
            judgeJson: """{"finalRiskLevel":"Moderate","finalConfidence":0.5,"requiresReview":true,"reviewReasons":[],"synthesis":"Partial."}""",
            challengerBlocked: true);
        var riskResult = CreateRiskResult(confidence: 0.5);

        var result = await agent.DebateAsync(riskResult, "Test note");

        result.Rounds[0].ChallengerArgument.Should().Be("[Content filter blocked]");
        result.ReviewReasons.Should().Contain("Challenger response blocked by content filter");
    }

    [Fact]
    public async Task DebateAsync_ContentFilterOnJudge_Throws()
    {
        var (agent, _) = CreateDebateAgent(judgeJson: null, judgeBlocked: true);
        var riskResult = CreateRiskResult(confidence: 0.5);

        var act = () => agent.DebateAsync(riskResult, "Test note");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*content filter*");
    }

    // --- Prompt builder tests ---

    [Fact]
    public void BuildAdvocatePrompt_ContainsRiskJsonAndNote()
    {
        var result = RiskDebatePrompts.BuildAdvocatePrompt("{risk}", "note text");

        result.Should().Contain("{risk}");
        result.Should().Contain("note text");
        result.Should().Contain("Defend this assessment");
    }

    [Fact]
    public void BuildChallengerPrompt_ContainsRiskJsonAndNote()
    {
        var result = RiskDebatePrompts.BuildChallengerPrompt("{risk}", "note text");

        result.Should().Contain("{risk}");
        result.Should().Contain("note text");
        result.Should().Contain("incorrect");
    }

    [Fact]
    public void BuildAdvocateRebuttalPrompt_ContainsChallengerArgument()
    {
        var result = RiskDebatePrompts.BuildAdvocateRebuttalPrompt("challenger says X");

        result.Should().Contain("challenger says X");
        result.Should().Contain("Rebut");
    }

    [Fact]
    public void BuildChallengerRebuttalPrompt_ContainsAdvocateArgument()
    {
        var result = RiskDebatePrompts.BuildChallengerRebuttalPrompt("advocate says Y");

        result.Should().Contain("advocate says Y");
        result.Should().Contain("Rebut");
    }

    [Fact]
    public void BuildJudgePrompt_ContainsAllRoundsAndRisk()
    {
        var rounds = new List<DebateRound>
        {
            new(1, "adv1", "chall1"),
            new(2, "adv2", "chall2")
        };
        var result = RiskDebatePrompts.BuildJudgePrompt(rounds, "{riskJson}");

        result.Should().Contain("adv1");
        result.Should().Contain("chall1");
        result.Should().Contain("adv2");
        result.Should().Contain("chall2");
        result.Should().Contain("{riskJson}");
        result.Should().Contain("Round 1:");
        result.Should().Contain("Round 2:");
    }

    // --- MaxRounds and model override tests ---

    [Fact]
    public async Task DebateAsync_MaxRounds1_ProducesOneRoundNoRebuttal()
    {
        var options = new RiskDebateOptions { MaxRounds = 1 };
        var (agent, clients) = CreateDebateAgent(
            judgeJson: """{"finalRiskLevel":"Moderate","finalConfidence":0.6}""",
            debateOptions: options);
        var riskResult = CreateRiskResult(confidence: 0.5);

        var result = await agent.DebateAsync(riskResult, "Test note");

        result.Rounds.Should().HaveCount(1);
        result.Rounds[0].RoundNumber.Should().Be(1);
        // 2 opening calls + 1 judge = 3 total LLM calls
        result.LlmTraces.Should().HaveCount(3);
        // 3 calls * 10 input each = 30
        result.InputTokens.Should().Be(30);
    }

    [Fact]
    public async Task DebateAsync_ModelOverride_UsesOverriddenDeploymentName()
    {
        var options = new RiskDebateOptions
        {
            ChallengerModelOverride = "gpt-4.1-nano"
        };
        var (agent, clients) = CreateDebateAgent(
            judgeJson: """{"finalRiskLevel":"Moderate","finalConfidence":0.5}""",
            debateOptions: options);
        var riskResult = CreateRiskResult(confidence: 0.5);

        var result = await agent.DebateAsync(riskResult, "Test note");

        // Challenger model should reflect the override
        result.ChallengerModel.Should().Be("gpt-4.1-nano");
        // Factory should have been called with the overridden deployment name
        clients.Factory.Received().CreateChatClient(
            Arg.Is<ModelSelection>(s => s.DeploymentName == "gpt-4.1-nano"
                && s.Provider == ModelProvider.AzureAIServices));
    }

    [Fact]
    public void BuildJudgePrompt_SingleRound_ProducesValidPrompt()
    {
        var rounds = new List<DebateRound> { new(1, "advocate opening", "challenger opening") };
        var result = RiskDebatePrompts.BuildJudgePrompt(rounds, "{risk}");

        result.Should().Contain("Round 1:");
        result.Should().Contain("advocate opening");
        result.Should().Contain("challenger opening");
        result.Should().NotContain("Round 2:");
    }

    // --- Helpers ---

    private static (RiskDebateAgent Agent, MockClients Clients) CreateDebateAgent(
        string? judgeJson = null,
        bool advocateBlocked = false,
        bool challengerBlocked = false,
        bool judgeBlocked = false,
        RiskDebateOptions? debateOptions = null)
    {
        var advocateClient = Substitute.For<IChatClient>();
        var challengerClient = Substitute.For<IChatClient>();
        var judgeClient = Substitute.For<IChatClient>();

        SetupChatClient(advocateClient, "Advocate argument text", advocateBlocked);
        SetupChatClient(challengerClient, "Challenger argument text", challengerBlocked);
        SetupChatClient(judgeClient, judgeJson ?? """{"finalRiskLevel":"Moderate","finalConfidence":0.5}""", judgeBlocked);

        var clientFactory = Substitute.For<IAIFoundryClientFactory>();
        var modelRouter = Substitute.For<IModelRouter>();

        modelRouter.SelectModel(ModelTask.RiskDebateAdvocate)
            .Returns(new ModelSelection("gpt-4.1-nano", ModelProvider.AzureOpenAI));
        modelRouter.SelectModel(ModelTask.RiskDebateChallenger)
            .Returns(new ModelSelection("Mistral-Large-3", ModelProvider.AzureAIServices));
        modelRouter.SelectModel(ModelTask.RiskDebateJudge)
            .Returns(new ModelSelection("gpt-4.1-mini", ModelProvider.AzureOpenAI));

        clientFactory.CreateChatClient(Arg.Any<ModelSelection>())
            .Returns(args =>
            {
                var sel = args.Arg<ModelSelection>();
                return sel.DeploymentName switch
                {
                    "gpt-4.1-nano" => advocateClient,
                    "Mistral-Large-3" => challengerClient,
                    "gpt-4.1-mini" => judgeClient,
                    _ => advocateClient // fallback for overrides
                };
            });

        var optionsMonitor = Substitute.For<IOptionsMonitor<RiskDebateOptions>>();
        optionsMonitor.CurrentValue.Returns(debateOptions ?? new RiskDebateOptions());

        var logger = Substitute.For<ILogger<RiskDebateAgent>>();
        var agent = new RiskDebateAgent(clientFactory, modelRouter, optionsMonitor, logger);

        return (agent, new MockClients(advocateClient, challengerClient, judgeClient, clientFactory));
    }

    private static void SetupChatClient(IChatClient client, string responseText, bool blocked)
    {
        var finishReason = blocked ? ChatFinishReason.ContentFilter : ChatFinishReason.Stop;
        var response = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, blocked ? null : responseText)])
        {
            FinishReason = finishReason,
            Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 }
        };

        client.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(response);
    }

    private record MockClients(IChatClient Advocate, IChatClient Challenger, IChatClient Judge, IAIFoundryClientFactory Factory);

    private static RiskAssessmentResult CreateRiskResult(double confidence, bool requiresReview = false)
    {
        return new RiskAssessmentResult
        {
            RequiresReview = requiresReview,
            DeterminedRiskLevel = RiskLevelOverall.Moderate,
            FinalExtraction = new RiskAssessmentExtracted
            {
                RiskLevelOverall = new ExtractedField<RiskLevelOverall>
                {
                    Value = RiskLevelOverall.Moderate,
                    Confidence = confidence
                }
            }
        };
    }
}
