using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Helpers;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Agents.Tools;
using SessionSight.Core.Enums;

namespace SessionSight.Agents.Agents;

public interface IRiskDebateAgent : ISessionSightAgent
{
    Task<RiskDebateResult> DebateAsync(
        RiskAssessmentResult riskResult,
        string noteText,
        CancellationToken ct = default);
}

public partial class RiskDebateAgent : IRiskDebateAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly ILogger<RiskDebateAgent> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RiskDebateAgent(
        IAIFoundryClientFactory clientFactory,
        IModelRouter modelRouter,
        ILogger<RiskDebateAgent> logger)
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _logger = logger;
    }

    public string Name => "RiskDebateAgent";

    public async Task<RiskDebateResult> DebateAsync(
        RiskAssessmentResult riskResult,
        string noteText,
        CancellationToken ct = default)
    {
        var riskJson = JsonSerializer.Serialize(riskResult.FinalExtraction, JsonOptions);

        // Resolve 3 clients upfront
        var advocateSelection = _modelRouter.SelectModel(ModelTask.RiskDebateAdvocate);
        var challengerSelection = _modelRouter.SelectModel(ModelTask.RiskDebateChallenger);
        var judgeSelection = _modelRouter.SelectModel(ModelTask.RiskDebateJudge);

        var advocateClient = _clientFactory.CreateChatClient(advocateSelection);
        var challengerClient = _clientFactory.CreateChatClient(challengerSelection);
        var judgeClient = _clientFactory.CreateChatClient(judgeSelection);

        LogDebateStarting(_logger, riskResult.DeterminedRiskLevel.ToString());

        var result = new RiskDebateResult
        {
            AdvocateModel = advocateSelection.DeploymentName,
            ChallengerModel = challengerSelection.DeploymentName,
            JudgeModel = judgeSelection.DeploymentName
        };

        var traces = new List<LlmCallTrace>();
        var totalInput = 0;
        var totalOutput = 0;
        var totalTokens = 0;

        // Round 1: Opening arguments
        var (advocateArg1, trace1) = await CallAsync(
            advocateClient,
            RiskDebatePrompts.AdvocateSystemPrompt,
            RiskDebatePrompts.BuildAdvocatePrompt(riskJson, noteText),
            advocateSelection.DeploymentName, 0.3f, ct);
        AccumulateTokens(trace1, ref totalInput, ref totalOutput, ref totalTokens);
        traces.Add(trace1);

        var (challengerArg1, trace2) = await CallAsync(
            challengerClient,
            RiskDebatePrompts.ChallengerSystemPrompt,
            RiskDebatePrompts.BuildChallengerPrompt(riskJson, noteText),
            challengerSelection.DeploymentName, 0.3f, ct);
        AccumulateTokens(trace2, ref totalInput, ref totalOutput, ref totalTokens);
        traces.Add(trace2);

        result.Rounds.Add(new DebateRound(1,
            advocateArg1 ?? "[Content filter blocked]",
            challengerArg1 ?? "[Content filter blocked]"));

        // Round 2: Rebuttals
        var (advocateArg2, trace3) = await CallAsync(
            advocateClient,
            RiskDebatePrompts.AdvocateSystemPrompt,
            RiskDebatePrompts.BuildAdvocateRebuttalPrompt(challengerArg1 ?? "[No argument provided]"),
            advocateSelection.DeploymentName, 0.3f, ct);
        AccumulateTokens(trace3, ref totalInput, ref totalOutput, ref totalTokens);
        traces.Add(trace3);

        var (challengerArg2, trace4) = await CallAsync(
            challengerClient,
            RiskDebatePrompts.ChallengerSystemPrompt,
            RiskDebatePrompts.BuildChallengerRebuttalPrompt(advocateArg1 ?? "[No argument provided]"),
            challengerSelection.DeploymentName, 0.3f, ct);
        AccumulateTokens(trace4, ref totalInput, ref totalOutput, ref totalTokens);
        traces.Add(trace4);

        result.Rounds.Add(new DebateRound(2,
            advocateArg2 ?? "[Content filter blocked]",
            challengerArg2 ?? "[Content filter blocked]"));

        // Track content filter blocks as review reasons
        if (advocateArg1 is null || advocateArg2 is null)
            result.ReviewReasons.Add("Advocate response blocked by content filter");
        if (challengerArg1 is null || challengerArg2 is null)
            result.ReviewReasons.Add("Challenger response blocked by content filter");

        // Judge synthesizes final verdict
        var transcript = new RiskDebatePrompts.DebateTranscript(
            advocateArg1 ?? "[Content filter blocked]",
            challengerArg1 ?? "[Content filter blocked]",
            advocateArg2 ?? "[Content filter blocked]",
            challengerArg2 ?? "[Content filter blocked]");

        var (judgeText, trace5) = await CallJsonAsync(
            judgeClient,
            RiskDebatePrompts.JudgeSystemPrompt,
            RiskDebatePrompts.BuildJudgePrompt(transcript, riskJson),
            judgeSelection.DeploymentName, 0.1f, ct);
        AccumulateTokens(trace5, ref totalInput, ref totalOutput, ref totalTokens);
        traces.Add(trace5);

        // Parse judge verdict
        var verdict = ParseJudgeVerdict(judgeText);
        result.FinalRiskLevel = verdict.FinalRiskLevel;
        result.FinalConfidence = verdict.FinalConfidence;
        result.RequiresReview = verdict.RequiresReview;
        result.ReviewReasons.AddRange(verdict.ReviewReasons);
        result.JudgeSynthesis = verdict.Synthesis;

        result.InputTokens = totalInput;
        result.OutputTokens = totalOutput;
        result.TotalTokens = totalTokens;
        result.LlmTraces = traces;

        LogDebateCompleted(_logger, result.FinalRiskLevel.ToString(), result.FinalConfidence);

        return result;
    }

    private async Task<(string? Text, LlmCallTrace Trace)> CallAsync(
        IChatClient client,
        string systemPrompt,
        string userPrompt,
        string modelName,
        float temperature,
        CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            Temperature = temperature,
            MaxOutputTokens = 1024
        };

        var sw = Stopwatch.StartNew();
        var response = await client.GetResponseAsync(messages, options, ct);
        sw.Stop();

        string? text = null;
        if (ContentFilterHelper.IsContentFilterBlocked(response))
        {
            LogContentFilterBlocked(_logger, modelName);
        }
        else
        {
            text = response.Text;
        }

        var inputTokens = (int)(response.Usage?.InputTokenCount ?? 0);
        var outputTokens = (int)(response.Usage?.OutputTokenCount ?? 0);
        var totalTokens = (int)(response.Usage?.TotalTokenCount ?? 0);

        var trace = new LlmCallTrace(
            PromptText: null,
            PromptSegmentsJson: AgentLoopRunner.SerializeDeltaSegments(messages, 0),
            ResponseText: text,
            ModelUsed: modelName,
            LoopRound: 0,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            TotalTokens: totalTokens,
            DurationMs: sw.ElapsedMilliseconds);

        return (text, trace);
    }

    private async Task<(string Text, LlmCallTrace Trace)> CallJsonAsync(
        IChatClient client,
        string systemPrompt,
        string userPrompt,
        string modelName,
        float temperature,
        CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            Temperature = temperature,
            MaxOutputTokens = 1024,
            ResponseFormat = ChatResponseFormat.Json
        };

        var sw = Stopwatch.StartNew();
        var response = await client.GetResponseAsync(messages, options, ct);
        sw.Stop();

        if (ContentFilterHelper.IsContentFilterBlocked(response))
        {
            LogContentFilterBlocked(_logger, modelName);
            throw new InvalidOperationException(
                $"Judge response blocked by content filter (model: {modelName})");
        }

        var text = response.Text!;
        var inputTokens = (int)(response.Usage?.InputTokenCount ?? 0);
        var outputTokens = (int)(response.Usage?.OutputTokenCount ?? 0);
        var totalTokens = (int)(response.Usage?.TotalTokenCount ?? 0);

        var trace = new LlmCallTrace(
            PromptText: null,
            PromptSegmentsJson: AgentLoopRunner.SerializeDeltaSegments(messages, 0),
            ResponseText: text,
            ModelUsed: modelName,
            LoopRound: 0,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            TotalTokens: totalTokens,
            DurationMs: sw.ElapsedMilliseconds);

        return (text, trace);
    }

    internal static JudgeVerdict ParseJudgeVerdict(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var riskLevel = RiskLevelOverall.Moderate;
            if (root.TryGetProperty("finalRiskLevel", out var rlProp)
                && Enum.TryParse<RiskLevelOverall>(rlProp.GetString(), ignoreCase: true, out var parsed))
            {
                riskLevel = parsed;
            }

            var confidence = root.TryGetProperty("finalConfidence", out var confProp)
                ? confProp.GetDouble()
                : 0.5;

            var requiresReview = root.TryGetProperty("requiresReview", out var rrProp) && rrProp.GetBoolean();

            var reviewReasons = new List<string>();
            if (root.TryGetProperty("reviewReasons", out var rrsProp) && rrsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in rrsProp.EnumerateArray())
                {
                    var reason = item.GetString();
                    if (!string.IsNullOrWhiteSpace(reason))
                        reviewReasons.Add(reason);
                }
            }

            var synthesis = root.TryGetProperty("synthesis", out var synProp)
                ? synProp.GetString() ?? string.Empty
                : string.Empty;

            return new JudgeVerdict(riskLevel, confidence, requiresReview, reviewReasons, synthesis);
        }
        catch (JsonException)
        {
            // Fallback: if JSON parse fails, return moderate with low confidence and flag for review
            return new JudgeVerdict(
                RiskLevelOverall.Moderate, 0.3, true,
                ["Judge response was not valid JSON"], string.Empty);
        }
    }

    private static void AccumulateTokens(LlmCallTrace trace, ref int input, ref int output, ref int total)
    {
        input += trace.InputTokens;
        output += trace.OutputTokens;
        total += trace.TotalTokens;
    }

    internal record JudgeVerdict(
        RiskLevelOverall FinalRiskLevel,
        double FinalConfidence,
        bool RequiresReview,
        List<string> ReviewReasons,
        string Synthesis);

    [LoggerMessage(Level = LogLevel.Information, Message = "Risk debate starting for risk level {RiskLevel}")]
    private static partial void LogDebateStarting(ILogger logger, string riskLevel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Risk debate completed: {FinalRiskLevel} (confidence: {Confidence:F2})")]
    private static partial void LogDebateCompleted(ILogger logger, string finalRiskLevel, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Content filter blocked response from {ModelName}")]
    private static partial void LogContentFilterBlocked(ILogger logger, string modelName);
}
