using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
using SessionSight.Agents.Tools;
using SessionSight.Agents.Validation;
using SessionSight.Core.Enums;
using SessionSight.Core.Schema;
using SessionSight.Core.ValueObjects;

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
    /// <returns>Extraction result with all clinical sections.</returns>
    Task<ExtractionResult> ExtractAsync(IntakeResult intake, CancellationToken cancellationToken = default);
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

    public async Task<ExtractionResult> ExtractAsync(IntakeResult intake, CancellationToken cancellationToken = default)
    {
        var noteText = intake.Document.MarkdownContent;
        var sessionId = Guid.NewGuid().ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        LogStartingClinicalExtraction(_logger, sessionId);

        var modelName = _modelRouter.SelectModel(ModelTask.Extraction);
        var chatClient = _clientFactory.CreateChatClient(modelName);

        // Build initial messages with extraction prompt
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(ExtractionPrompts.SystemPrompt),
            new UserChatMessage($"""
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

        // Run agent loop
        var loopResult = await _agentLoopRunner.RunAsync(chatClient, messages, cancellationToken);

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
                ToolCallCount = loopResult.ToolCallCount
            };
        }

        // Parse the final extraction from agent response
        var extraction = ParseExtractionResponse(loopResult.Content);

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
            ToolCallCount = loopResult.ToolCallCount
        };
    }

    private ClinicalExtraction ParseExtractionResponse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            LogEmptyExtractionResponse(_logger);
            return new ClinicalExtraction();
        }

        try
        {
            // Extract JSON from response (may be wrapped in markdown code block)
            var json = ExtractJson(content);
            return JsonSerializer.Deserialize<ClinicalExtraction>(json, JsonOptions) ?? new ClinicalExtraction();
        }
        catch (JsonException ex)
        {
            LogJsonParseFailure(_logger, ex);
            return new ClinicalExtraction();
        }
    }

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

    internal static T ParseSectionResponse<T>(string sectionName, string content) where T : new()
    {
        var json = ExtractJson(content);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
            if (parsed == null)
            {
                return new T();
            }

            return MapToSection<T>(parsed);
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    internal static string ExtractJson(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIndex > 7)
            {
                return trimmed[7..endIndex].Trim();
            }
        }

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var startIndex = trimmed.IndexOf('\n') + 1;
            var endIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                return trimmed[startIndex..endIndex].Trim();
            }
        }

        return trimmed;
    }

    private static T MapToSection<T>(Dictionary<string, JsonElement> parsed) where T : new()
    {
        var section = new T();
        var properties = typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            // Convert PascalCase property name to camelCase for JSON lookup
            var jsonKey = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];

            if (!parsed.TryGetValue(jsonKey, out var element))
                continue;

            if (!prop.PropertyType.IsGenericType ||
                prop.PropertyType.GetGenericTypeDefinition() != typeof(ExtractedField<>))
                continue;

            var extractedField = MapToExtractedField(prop.PropertyType, element);
            if (extractedField != null)
            {
                prop.SetValue(section, extractedField);
            }
        }

        return section;
    }

    private static object? MapToExtractedField(Type fieldType, JsonElement element)
    {
        var valueType = fieldType.GetGenericArguments()[0];
        var field = Activator.CreateInstance(fieldType);
        if (field == null) return null;

        var valueProperty = fieldType.GetProperty("Value");
        var confidenceProperty = fieldType.GetProperty("Confidence");
        var sourceProperty = fieldType.GetProperty("Source");

        if (element.TryGetProperty("value", out var valueElement))
        {
            var value = DeserializeValue(valueElement, valueType);
            valueProperty?.SetValue(field, value);
        }

        if (element.TryGetProperty("confidence", out var confElement) && confElement.TryGetDouble(out var conf))
        {
            confidenceProperty?.SetValue(field, conf);
        }

        if (element.TryGetProperty("source", out var sourceElement) && sourceElement.ValueKind != JsonValueKind.Null)
        {
            var source = DeserializeSourceMapping(sourceElement);
            sourceProperty?.SetValue(field, source);
        }

        return field;
    }

    private static object? DeserializeValue(JsonElement element, Type targetType)
    {
        if (element.ValueKind == JsonValueKind.Null)
            return null;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle enums
        if (underlyingType.IsEnum)
        {
            var stringValue = element.GetString();
            if (string.IsNullOrEmpty(stringValue))
                return null;
            return Enum.TryParse(underlyingType, stringValue, ignoreCase: true, out var result) ? result : null;
        }

        // Handle common types
        if (underlyingType == typeof(string))
            return element.GetString();

        if (underlyingType == typeof(int))
            return element.TryGetInt32(out var i) ? i : null;

        if (underlyingType == typeof(bool))
            return element.ValueKind == JsonValueKind.True;

        if (underlyingType == typeof(double))
            return element.TryGetDouble(out var d) ? d : null;

        if (underlyingType == typeof(DateOnly))
        {
            var dateStr = element.GetString();
            return DateOnly.TryParse(dateStr, out var date) ? date : null;
        }

        if (underlyingType == typeof(TimeOnly))
        {
            var timeStr = element.GetString();
            return TimeOnly.TryParse(timeStr, out var time) ? time : null;
        }

        // Handle List<string>
        if (underlyingType == typeof(List<string>))
        {
            if (element.ValueKind != JsonValueKind.Array)
                return new List<string>();

            var list = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                var str = item.GetString();
                if (str != null)
                    list.Add(str);
            }
            return list;
        }

        // Handle List<enum>
        if (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var itemType = underlyingType.GetGenericArguments()[0];
            if (itemType.IsEnum && element.ValueKind == JsonValueKind.Array)
            {
                var listType = typeof(List<>).MakeGenericType(itemType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                foreach (var item in element.EnumerateArray())
                {
                    var str = item.GetString();
                    if (!string.IsNullOrEmpty(str) && Enum.TryParse(itemType, str, ignoreCase: true, out var enumValue))
                    {
                        list.Add(enumValue);
                    }
                }
                return list;
            }
        }

        // Handle Dictionary<string, string>
        if (underlyingType == typeof(Dictionary<string, string>))
        {
            if (element.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string>();

            var dict = new Dictionary<string, string>();
            foreach (var prop in element.EnumerateObject())
            {
                var val = prop.Value.GetString();
                if (val != null)
                    dict[prop.Name] = val;
            }
            return dict;
        }

        return null;
    }

    private static SourceMapping? DeserializeSourceMapping(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var mapping = new SourceMapping();

        if (element.TryGetProperty("text", out var textElement))
            mapping.Text = textElement.GetString() ?? string.Empty;

        if (element.TryGetProperty("startChar", out var startElement) && startElement.TryGetInt32(out var start))
            mapping.StartChar = start;

        if (element.TryGetProperty("endChar", out var endElement) && endElement.TryGetInt32(out var end))
            mapping.EndChar = end;

        if (element.TryGetProperty("section", out var sectionElement))
            mapping.Section = sectionElement.GetString();

        return mapping;
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
    private static partial void LogJsonParseFailure(ILogger logger, Exception exception);
}
