using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Helpers;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Agents.Tools;
using SessionSight.Agents.Validation;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Agents;

/// <summary>
/// Interface for the Clinical Extractor Agent.
/// </summary>
public interface IClinicalExtractorAgent : ISessionSightAgent
{
    /// <summary>
    /// Extracts clinical data from a validated therapy note.
    /// </summary>
    /// <param name="intake">The intake result containing the parsed document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="onRoundComplete">Optional callback invoked after each agent loop round for incremental trace saving.</param>
    /// <returns>Extraction result with all clinical sections.</returns>
    Task<ExtractionResult> ExtractAsync(
        IntakeResult intake,
        Func<LlmCallTrace, IReadOnlyList<ToolCallEntry>, Task>? onRoundComplete = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Clinical Extractor Agent implementation.
/// Extracts 82 fields from therapy notes using an agent loop pattern with tools.
/// </summary>
public partial class ClinicalExtractorAgent : IClinicalExtractorAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly ISchemaValidator _validator;
    private readonly AgentLoopRunner _agentLoopRunner;
    private readonly ILogger<ClinicalExtractorAgent> _logger;

    public ClinicalExtractorAgent(
        IAIFoundryClientFactory clientFactory,
        IModelRouter modelRouter,
        ISchemaValidator validator,
        AgentLoopRunner agentLoopRunner,
        ILogger<ClinicalExtractorAgent> logger)
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _validator = validator;
        _agentLoopRunner = agentLoopRunner;
        _logger = logger;
    }

    public string Name => "ClinicalExtractorAgent";

    public async Task<ExtractionResult> ExtractAsync(
        IntakeResult intake,
        Func<LlmCallTrace, IReadOnlyList<ToolCallEntry>, Task>? onRoundComplete = null,
        CancellationToken cancellationToken = default)
    {
        var noteText = intake.Document.MarkdownContent;
        var sessionId = Guid.NewGuid().ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        LogStartingClinicalExtraction(_logger, sessionId);

        var modelName = _modelRouter.SelectModel(ModelTask.Extraction);
        var chatClient = _clientFactory.CreateChatClient(modelName);

        // Build initial messages with extraction prompt
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ExtractionPrompts.SystemPrompt),
            new(ChatRole.User, $"""
                Extract clinical data from the following therapy note.

                Use the available tools to:
                1. Validate your extraction against the schema
                2. Score confidence on your extraction
                3. Check for risk keywords in the original text
                4. Look up diagnosis codes if present

                Return a complete JSON extraction when done.

                --- THERAPY NOTE ---
                {noteText}
                """)
        };

        // JSON response format guarantees valid JSON from the API (see also: ExtractionPrompts.SystemPrompt CRITICAL instruction)
        var loopResult = await _agentLoopRunner.RunAsync(
            chatClient, messages, ChatResponseFormat.Json, temperature: 0.1f,
            onRoundComplete: onRoundComplete, ct: cancellationToken);

        LogAgentLoopCompleted(_logger, loopResult.ToolCallCount, loopResult.IsComplete);

        if (loopResult.IsPartial)
        {
            LogExtractionIncomplete(_logger, loopResult.PartialReason);

            return new ExtractionResult
            {
                SessionId = sessionId,
                Data = new ClinicalExtraction(),
                RequiresReview = true,
                LowConfidenceFields = [loopResult.PartialReason ?? "Extraction incomplete"],
                ModelsUsed = [modelName],
                Errors = [$"Partial extraction: {loopResult.PartialReason}"],
                ToolCallCount = loopResult.ToolCallCount,
                InputTokens = loopResult.InputTokens,
                OutputTokens = loopResult.OutputTokens,
                TotalTokens = loopResult.TotalTokens,
                ToolCallTrace = loopResult.ToolCallTrace
            };
        }

        // Parse the final extraction from agent response
        var extraction = ParseExtractionResponse(loopResult.Content);

        if (extraction is null)
        {
            LogJsonParseReturnedNull(_logger);
        }

        if (extraction is null)
        {
            return new ExtractionResult
            {
                SessionId = sessionId,
                Data = new ClinicalExtraction(),
                RequiresReview = true,
                Errors = ["Failed to parse extraction JSON from agent response"],
                ModelsUsed = [modelName],
                ToolCallCount = loopResult.ToolCallCount,
                InputTokens = loopResult.InputTokens,
                OutputTokens = loopResult.OutputTokens,
                TotalTokens = loopResult.TotalTokens,
                ToolCallTrace = loopResult.ToolCallTrace,
                LlmTraces = loopResult.LlmTraces
            };
        }

        // Final validation and confidence scoring
        var validationResult = _validator.Validate(extraction);
        var confidence = ConfidenceCalculator.Calculate(extraction);
        var lowConfidenceFields = ConfidenceCalculator.GetLowConfidenceFields(extraction);
        var hasLowConfidenceRisk = ConfidenceCalculator.HasLowConfidenceRiskFields(extraction);

        // Set metadata
        extraction.Metadata = new ExtractionMetadata
        {
            ExtractionTimestamp = DateTime.UtcNow,
            ExtractionModel = modelName,
            ExtractionVersion = "1.0.0",
            OverallConfidence = confidence,
            LowConfidenceFields = lowConfidenceFields,
            RequiresReview = !validationResult.IsValid || hasLowConfidenceRisk
        };

        LogClinicalExtractionCompleted(_logger, sessionId, confidence, extraction.Metadata.RequiresReview);

        return new ExtractionResult
        {
            SessionId = sessionId,
            Data = extraction,
            OverallConfidence = confidence,
            RequiresReview = extraction.Metadata.RequiresReview,
            LowConfidenceFields = lowConfidenceFields,
            ModelsUsed = [modelName],
            Errors = validationResult.Errors.Select(e => e.Message).ToList(),
            ToolCallCount = loopResult.ToolCallCount,
            InputTokens = loopResult.InputTokens,
            OutputTokens = loopResult.OutputTokens,
            TotalTokens = loopResult.TotalTokens,
            ToolCallTrace = loopResult.ToolCallTrace,
            LlmTraces = loopResult.LlmTraces
        };
    }

    private ClinicalExtraction? ParseExtractionResponse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            LogEmptyExtractionResponse(_logger);
            return null;
        }

        var json = LlmJsonHelper.ExtractJson(content);
        var result = LlmExtractionParser.Parse(json);

        if (result is null)
            LogJsonParseFailure(_logger, null);
        else if (result.SessionInfo != null)
            LogLenientParseUsed(_logger);

        return result;
    }

    // Test-only helper — used by 25+ tests in ClinicalExtractorAgentTests
    internal static string GetPromptForSection(string sectionName, string noteText)
    {
        return sectionName switch
        {
            "SessionInfo" => ExtractionPrompts.GetSessionInfoPrompt(noteText),
            "PresentingConcerns" => ExtractionPrompts.GetPresentingConcernsPrompt(noteText),
            "MoodAssessment" => ExtractionPrompts.GetMoodAssessmentPrompt(noteText),
            "RiskAssessment" => ExtractionPrompts.GetRiskAssessmentPrompt(noteText),
            "MentalStatusExam" => ExtractionPrompts.GetMentalStatusExamPrompt(noteText),
            "Interventions" => ExtractionPrompts.GetInterventionsPrompt(noteText),
            "Diagnoses" => ExtractionPrompts.GetDiagnosesPrompt(noteText),
            "TreatmentProgress" => ExtractionPrompts.GetTreatmentProgressPrompt(noteText),
            "NextSteps" => ExtractionPrompts.GetNextStepsPrompt(noteText),
            _ => throw new ArgumentException($"Unknown section: {sectionName}")
        };
    }

    // Test-only helper — used by 50+ tests in ClinicalExtractorAgentTests
    internal static T ParseSectionResponse<T>(string sectionName, string content) where T : new()
    {
        var json = LlmJsonHelper.ExtractJson(content);
        return LlmExtractionParser.ParseSection<T>(json);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting clinical extraction for session {SessionId}")]
    private static partial void LogStartingClinicalExtraction(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent loop completed with {ToolCalls} tool calls, IsComplete={IsComplete}")]
    private static partial void LogAgentLoopCompleted(ILogger logger, int toolCalls, bool isComplete);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extraction incomplete: {Reason}")]
    private static partial void LogExtractionIncomplete(ILogger logger, string? reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Clinical extraction completed for session {SessionId}. Confidence: {Confidence:F2}, RequiresReview: {RequiresReview}")]
    private static partial void LogClinicalExtractionCompleted(ILogger logger, string sessionId, double confidence, bool requiresReview);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Empty extraction response from agent")]
    private static partial void LogEmptyExtractionResponse(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse extraction response as JSON")]
    private static partial void LogJsonParseFailure(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Extraction JSON parse returned null - malformed response")]
    private static partial void LogJsonParseReturnedNull(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Used lenient JSON parsing for extraction response")]
    private static partial void LogLenientParseUsed(ILogger logger);
}
