using System.Text.Json;
using Azure.AI.Inference;
using Microsoft.Extensions.Logging;
using SessionSight.Agents.Models;
using SessionSight.Agents.Prompts;
using SessionSight.Agents.Routing;
using SessionSight.Agents.Services;
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
/// Extracts 82 fields from therapy notes using parallel LLM calls.
/// </summary>
public class ClinicalExtractorAgent : IClinicalExtractorAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly ISchemaValidator _validator;
    private readonly ConfidenceCalculator _confidenceCalculator;
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
        ConfidenceCalculator confidenceCalculator,
        ILogger<ClinicalExtractorAgent> logger)
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _validator = validator;
        _confidenceCalculator = confidenceCalculator;
        _logger = logger;
    }

    public string Name => "ClinicalExtractorAgent";

    public async Task<ExtractionResult> ExtractAsync(IntakeResult intake, CancellationToken cancellationToken = default)
    {
        var noteText = intake.Document.MarkdownContent;
        var sessionId = Guid.NewGuid().ToString();
        var modelsUsed = new HashSet<string>();
        var errors = new List<string>();

        _logger.LogInformation("Starting clinical extraction for session {SessionId}", sessionId);

        // Run all 9 section extractions in parallel
        var tasks = new[]
        {
            ExtractSectionAsync<SessionInfoExtracted>("SessionInfo", noteText, ModelTask.ExtractionSimple, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<PresentingConcernsExtracted>("PresentingConcerns", noteText, ModelTask.ExtractionSimple, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<MoodAssessmentExtracted>("MoodAssessment", noteText, ModelTask.Extraction, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<RiskAssessmentExtracted>("RiskAssessment", noteText, ModelTask.RiskAssessment, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<MentalStatusExamExtracted>("MentalStatusExam", noteText, ModelTask.Extraction, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<InterventionsExtracted>("Interventions", noteText, ModelTask.ExtractionSimple, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<DiagnosesExtracted>("Diagnoses", noteText, ModelTask.Extraction, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<TreatmentProgressExtracted>("TreatmentProgress", noteText, ModelTask.ExtractionSimple, modelsUsed, errors, cancellationToken),
            ExtractSectionAsync<NextStepsExtracted>("NextSteps", noteText, ModelTask.ExtractionSimple, modelsUsed, errors, cancellationToken)
        };

        var results = await Task.WhenAll(tasks);

        // Merge results into ClinicalExtraction
        var extraction = new ClinicalExtraction
        {
            SessionInfo = (SessionInfoExtracted)results[0],
            PresentingConcerns = (PresentingConcernsExtracted)results[1],
            MoodAssessment = (MoodAssessmentExtracted)results[2],
            RiskAssessment = (RiskAssessmentExtracted)results[3],
            MentalStatusExam = (MentalStatusExamExtracted)results[4],
            Interventions = (InterventionsExtracted)results[5],
            Diagnoses = (DiagnosesExtracted)results[6],
            TreatmentProgress = (TreatmentProgressExtracted)results[7],
            NextSteps = (NextStepsExtracted)results[8]
        };

        // Validate and calculate confidence
        var validation = _validator.Validate(extraction);
        var confidence = _confidenceCalculator.Calculate(extraction);
        var lowConfidenceFields = _confidenceCalculator.GetLowConfidenceFields(extraction);
        var hasLowConfidenceRisk = _confidenceCalculator.HasLowConfidenceRiskFields(extraction);

        // Set metadata
        extraction.Metadata = new ExtractionMetadata
        {
            ExtractionTimestamp = DateTime.UtcNow,
            ExtractionModel = string.Join(", ", modelsUsed),
            ExtractionVersion = "1.0.0",
            OverallConfidence = confidence,
            LowConfidenceFields = lowConfidenceFields,
            RequiresReview = !validation.IsValid || hasLowConfidenceRisk
        };

        _logger.LogInformation(
            "Clinical extraction completed for session {SessionId}. Confidence: {Confidence:F2}, RequiresReview: {RequiresReview}",
            sessionId, confidence, extraction.Metadata.RequiresReview);

        return new ExtractionResult
        {
            SessionId = sessionId,
            Data = extraction,
            OverallConfidence = confidence,
            RequiresReview = extraction.Metadata.RequiresReview,
            LowConfidenceFields = lowConfidenceFields,
            ModelsUsed = modelsUsed.ToList(),
            Errors = errors
        };
    }

    private async Task<object> ExtractSectionAsync<T>(
        string sectionName,
        string noteText,
        ModelTask modelTask,
        HashSet<string> modelsUsed,
        List<string> errors,
        CancellationToken cancellationToken) where T : new()
    {
        var modelName = _modelRouter.SelectModel(modelTask);
        lock (modelsUsed)
        {
            modelsUsed.Add(modelName);
        }

        _logger.LogDebug("Extracting {Section} with {Model}", sectionName, modelName);

        try
        {
            var chatClient = _clientFactory.CreateChatClient();
            var prompt = GetPromptForSection(sectionName, noteText);

            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage("You are a clinical extraction assistant. Extract structured data from therapy notes accurately."),
                new ChatRequestUserMessage(prompt)
            };

            var options = new ChatCompletionsOptions(messages)
            {
                Model = modelName,
                Temperature = 0.1f,
                MaxTokens = 2048
            };

            var response = await chatClient.CompleteAsync(options, cancellationToken);
            var content = response.Value.Content;

            return ParseSectionResponse<T>(sectionName, content) ?? new T();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting {Section}", sectionName);
            lock (errors)
            {
                errors.Add($"{sectionName}: {ex.Message}");
            }
            return new T();
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

        if (trimmed.StartsWith("```"))
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
}
