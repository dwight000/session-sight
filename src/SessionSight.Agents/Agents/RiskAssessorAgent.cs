using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SessionSight.Agents.Helpers;
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
/// Interface for the Risk Assessor Agent.
/// </summary>
public interface IRiskAssessorAgent : ISessionSightAgent
{
    /// <summary>
    /// Assesses and validates risk extraction from a clinical note.
    /// </summary>
    /// <param name="extraction">The extraction result from the Clinical Extractor.</param>
    /// <param name="originalNoteText">The original therapy note text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Risk assessment result with validation and any discrepancies.</returns>
    Task<RiskAssessmentResult> AssessAsync(
        ExtractionResult extraction,
        string originalNoteText,
        CancellationToken ct = default);
}

/// <summary>
/// Risk Assessor Agent implementation.
/// Provides safety-critical second-pass validation of risk assessment fields.
/// </summary>
public partial class RiskAssessorAgent : IRiskAssessorAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly RiskAssessorOptions _options;
    private readonly ILogger<RiskAssessorAgent> _logger;

    private const double RiskConfidenceThreshold = 0.9;
    private const string FieldSuicidalIdeation = "SuicidalIdeation";
    private const string FieldSelfHarm = "SelfHarm";
    private const string FieldHomicidalIdeation = "HomicidalIdeation";
    private const string FieldRiskLevelOverall = "RiskLevelOverall";
    private static readonly string[] RequiredDiagnosticKeys =
    [
        "suicidal_ideation",
        "si_frequency",
        "self_harm",
        "homicidal_ideation",
        "risk_level_overall"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RiskAssessorAgent(
        IAIFoundryClientFactory clientFactory,
        IModelRouter modelRouter,
        IOptions<RiskAssessorOptions> options,
        ILogger<RiskAssessorAgent> logger)
    {
        _clientFactory = clientFactory;
        _modelRouter = modelRouter;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "RiskAssessorAgent";

    public async Task<RiskAssessmentResult> AssessAsync(
        ExtractionResult extraction,
        string originalNoteText,
        CancellationToken ct = default)
    {
        LogStartingRiskAssessment(_logger, extraction.SessionId);

        var result = new RiskAssessmentResult
        {
            OriginalExtraction = extraction.Data.RiskAssessment
        };
        KeywordCheckResult? keywordResult = null;
        var criteriaUsed = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var reasoningUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var criteriaValidationAttemptsUsed = 1;

        // Step 1: Re-extract with focused safety prompt
        var modelName = _modelRouter.SelectModel(ModelTask.RiskAssessment);
        result.ModelUsed = modelName;

        try
        {
            var reExtracted = await ReExtractRiskAsync(originalNoteText, modelName, ct);
            result.ValidatedExtraction = reExtracted.Risk;
            criteriaUsed = reExtracted.CriteriaUsed;
            reasoningUsed = reExtracted.ReasoningUsed;
            criteriaValidationAttemptsUsed = Math.Max(1, reExtracted.CriteriaValidationAttemptsUsed);
        }
        catch (Exception ex)
        {
            if (_options.RequireCriteriaUsed && ex is MissingDiagnosticFeedbackException)
            {
                throw;
            }

            LogRiskReExtractionError(_logger, ex, extraction.SessionId);
            result.ValidatedExtraction = new RiskAssessmentExtracted();
            result.ReviewReasons.Add($"Re-extraction failed: {ex.Message}");
            result.RequiresReview = true;
        }

        // Step 2: Check for danger keywords (safety net)
        if (_options.EnableKeywordSafetyNet)
        {
            keywordResult = DangerKeywordChecker.Check(originalNoteText);
            result.KeywordMatches = keywordResult.AllMatches;

            // Flag if keywords found but extraction shows "None"
            CheckKeywordMismatch(result, keywordResult);
        }

        // Step 3: Find discrepancies between original and re-extracted
        result.Discrepancies = FindDiscrepancies(
            result.OriginalExtraction,
            result.ValidatedExtraction);

        // Step 4: Create final merged extraction (conservative merge)
        result.FinalExtraction = _options.UseConservativeMerge
            ? ConservativeMerge(result.OriginalExtraction, result.ValidatedExtraction)
            : result.ValidatedExtraction;

        var homicidalGuardrail = ApplyHomicidalEvidenceGuardrail(result, keywordResult);
        var selfHarmGuardrail = ApplySelfHarmEvidenceGuardrail(result, keywordResult, criteriaUsed);

        result.Diagnostics = BuildDiagnostics(
            result.OriginalExtraction,
            result.ValidatedExtraction,
            result.FinalExtraction,
            result.Discrepancies,
            keywordResult,
            homicidalGuardrail.Applied,
            homicidalGuardrail.Reason,
            selfHarmGuardrail.Applied,
            selfHarmGuardrail.Reason,
            criteriaUsed,
            reasoningUsed,
            criteriaValidationAttemptsUsed);

        result.DeterminedRiskLevel = result.FinalExtraction.RiskLevelOverall.Value;

        // Step 5: Determine if review is required
        DetermineReviewRequirements(result);

        LogRiskAssessmentCompleted(_logger, extraction.SessionId, result.RequiresReview, result.DeterminedRiskLevel, result.Discrepancies.Count);

        return result;
    }

    private async Task<RiskReExtractionResponse> ReExtractRiskAsync(
        string noteText,
        string modelName,
        CancellationToken ct)
    {
        var chatClient = _clientFactory.CreateChatClient(modelName);
        var basePrompt = RiskPrompts.GetRiskReExtractionPrompt(noteText);
        var attempts = Math.Max(1, _options.CriteriaValidationAttempts);
        List<string>? lastMissingCriteria = null;
        List<string>? lastMissingReasoning = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var prompt = attempt == 1
                ? basePrompt
                : $"{basePrompt}\n\nRETRY REQUIREMENT: Include non-empty criteria_used arrays and non-empty reasoning_used strings for all required keys.";
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(RiskPrompts.SystemPrompt),
                new UserChatMessage(prompt)
            };

            // JSON response format guarantees valid JSON from the API (see also: RiskPrompts.SystemPrompt CRITICAL instruction)
            var options = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                MaxOutputTokenCount = 2048,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await chatClient.CompleteChatAsync(messages, options, ct);
            var content = response.Value.Content[0].Text;
            var parsed = ParseRiskResponseWithCriteria(content)
                ?? throw new InvalidOperationException("Failed to parse risk re-extraction response");
            parsed.CriteriaValidationAttemptsUsed = attempt;

            if (!_options.RequireCriteriaUsed)
            {
                return parsed;
            }

            var hasCriteria = HasRequiredCriteriaUsed(parsed.CriteriaUsed, out var missingCriteriaKeys);
            var hasReasoning = HasRequiredReasoningUsed(parsed.ReasoningUsed, out var missingReasoningKeys);
            if (hasCriteria && hasReasoning)
            {
                return parsed;
            }

            lastMissingCriteria = missingCriteriaKeys;
            lastMissingReasoning = missingReasoningKeys;
        }

        throw new MissingDiagnosticFeedbackException(lastMissingCriteria ?? [], lastMissingReasoning ?? []);
    }

    internal static RiskAssessmentExtracted? ParseRiskResponse(string content)
    {
        return ParseRiskResponseWithCriteria(content)?.Risk;
    }

    internal static RiskReExtractionResponse? ParseRiskResponseWithCriteria(string content)
    {
        var json = ExtractJson(content);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
            if (parsed == null)
            {
                return null;
            }

            return new RiskReExtractionResponse
            {
                Risk = MapToRiskAssessment(parsed),
                CriteriaUsed = ParseCriteriaUsed(parsed),
                ReasoningUsed = ParseReasoningUsed(parsed)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string ExtractJson(string content) => LlmJsonHelper.ExtractJson(content);

    private static RiskAssessmentExtracted MapToRiskAssessment(Dictionary<string, JsonElement> parsed)
    {
        var section = new RiskAssessmentExtracted();
        var properties = typeof(RiskAssessmentExtracted).GetProperties();

        foreach (var prop in properties)
        {
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

    private static Dictionary<string, List<string>> ParseCriteriaUsed(Dictionary<string, JsonElement> parsed)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!parsed.TryGetValue("criteria_used", out var criteriaElement) ||
            criteriaElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in criteriaElement.EnumerateObject())
        {
            var values = new List<string>();
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            values.Add(value.Trim());
                        }
                    }
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }

            if (values.Count > 0)
            {
                result[property.Name] = values;
            }
        }

        return result;
    }

    private static Dictionary<string, string> ParseReasoningUsed(Dictionary<string, JsonElement> parsed)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parsed.TryGetValue("reasoning_used", out var reasoningElement) ||
            reasoningElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in reasoningElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[property.Name] = NormalizeReasoning(value);
                }
            }
        }

        return result;
    }

    internal static bool HasRequiredCriteriaUsed(
        IReadOnlyDictionary<string, List<string>> criteriaUsed,
        out List<string> missingKeys)
    {
        missingKeys = [];
        foreach (var key in RequiredDiagnosticKeys)
        {
            if (!criteriaUsed.TryGetValue(key, out var values) ||
                values.Count == 0 ||
                values.All(static value => string.IsNullOrWhiteSpace(value)))
            {
                missingKeys.Add(key);
            }
        }

        return missingKeys.Count == 0;
    }

    internal static bool HasRequiredReasoningUsed(
        IReadOnlyDictionary<string, string> reasoningUsed,
        out List<string> missingKeys)
    {
        missingKeys = [];
        foreach (var key in RequiredDiagnosticKeys)
        {
            if (!reasoningUsed.TryGetValue(key, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                missingKeys.Add(key);
            }
        }

        return missingKeys.Count == 0;
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

        if (element.TryGetProperty("confidence", out var confElement))
        {
            var conf = LlmJsonHelper.TryParseConfidence(confElement);
            if (conf.HasValue)
                confidenceProperty?.SetValue(field, conf.Value);
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

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
            return DeserializeEnum(element, underlyingType);

        if (underlyingType == typeof(string))
            return element.GetString();

        if (underlyingType == typeof(bool))
            return element.ValueKind == JsonValueKind.True;

        if (underlyingType == typeof(List<string>))
            return DeserializeStringList(element);

        return null;
    }

    private static object? DeserializeEnum(JsonElement element, Type enumType)
    {
        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;
        return Enum.TryParse(enumType, stringValue, ignoreCase: true, out var result) ? result : null;
    }

    private static List<string> DeserializeStringList(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return [];

        return element.EnumerateArray()
            .Select(item => item.GetString())
            .Where(str => str != null)
            .ToList()!;
    }

    private static SourceMapping? DeserializeSourceMapping(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return new SourceMapping { Text = element.GetString() ?? string.Empty };

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

    internal static List<RiskDiscrepancy> FindDiscrepancies(
        RiskAssessmentExtracted original,
        RiskAssessmentExtracted reExtracted)
    {
        var discrepancies = new List<RiskDiscrepancy>();

        CheckDiscrepancy(discrepancies, FieldSuicidalIdeation,
            GetEnumString(original.SuicidalIdeation.Value),
            original.SuicidalIdeation.Confidence,
            GetEnumString(reExtracted.SuicidalIdeation.Value),
            reExtracted.SuicidalIdeation.Confidence);

        CheckDiscrepancy(discrepancies, FieldSelfHarm,
            GetEnumString(original.SelfHarm.Value),
            original.SelfHarm.Confidence,
            GetEnumString(reExtracted.SelfHarm.Value),
            reExtracted.SelfHarm.Confidence);

        CheckDiscrepancy(discrepancies, FieldHomicidalIdeation,
            GetEnumString(original.HomicidalIdeation.Value),
            original.HomicidalIdeation.Confidence,
            GetEnumString(reExtracted.HomicidalIdeation.Value),
            reExtracted.HomicidalIdeation.Confidence);

        CheckDiscrepancy(discrepancies, FieldRiskLevelOverall,
            GetEnumString(original.RiskLevelOverall.Value, "Low"),
            original.RiskLevelOverall.Confidence,
            GetEnumString(reExtracted.RiskLevelOverall.Value, "Low"),
            reExtracted.RiskLevelOverall.Confidence);

        return discrepancies;
    }

    private static string GetEnumString(SuicidalIdeation? value, string defaultValue = "None")
        => value?.ToString() ?? defaultValue;

    private static string GetEnumString(SelfHarm? value, string defaultValue = "None")
        => value?.ToString() ?? defaultValue;

    private static string GetEnumString(HomicidalIdeation? value, string defaultValue = "None")
        => value?.ToString() ?? defaultValue;

    private static string GetEnumString(RiskLevelOverall? value, string defaultValue = "Low")
        => value?.ToString() ?? defaultValue;

    private static void CheckDiscrepancy(
        List<RiskDiscrepancy> discrepancies,
        string fieldName,
        string originalValue,
        double originalConfidence,
        string reExtractedValue,
        double reExtractedConfidence)
    {
        if (!string.Equals(originalValue, reExtractedValue, StringComparison.OrdinalIgnoreCase))
        {
            var resolved = SelectMoreSevere(fieldName, originalValue, reExtractedValue);
            discrepancies.Add(new RiskDiscrepancy
            {
                FieldName = fieldName,
                OriginalValue = originalValue,
                OriginalConfidence = originalConfidence,
                ReExtractedValue = reExtractedValue,
                ReExtractedConfidence = reExtractedConfidence,
                ResolvedValue = resolved,
                ResolutionReason = $"Conservative merge: selected more severe value '{resolved}'"
            });
        }
    }

    internal static string SelectMoreSevere(string fieldName, string value1, string value2)
    {
        // Get severity scores - higher = more severe
        var severity1 = GetSeverityScore(fieldName, value1);
        var severity2 = GetSeverityScore(fieldName, value2);

        return severity1 >= severity2 ? value1 : value2;
    }

    internal static int GetSeverityScore(string fieldName, string value)
    {
        return fieldName switch
        {
            FieldSuicidalIdeation => value switch
            {
                "None" => 0,
                "Passive" => 1,
                "ActiveNoPlan" => 2,
                "ActiveWithPlan" => 3,
                "ActiveWithIntent" => 4,
                _ => 0
            },
            FieldSelfHarm => value switch
            {
                "None" => 0,
                "Historical" => 1,
                "Recent" => 2,
                "Current" => 3,
                "Imminent" => 4,
                _ => 0
            },
            FieldHomicidalIdeation => value switch
            {
                "None" => 0,
                "Passive" => 1,
                "ActiveNoPlan" => 2,
                "ActiveWithPlan" => 3,
                _ => 0
            },
            FieldRiskLevelOverall => value switch
            {
                "Low" => 0,
                "Moderate" => 1,
                "High" => 2,
                "Imminent" => 3,
                _ => 0
            },
            _ => 0
        };
    }

    internal static RiskAssessmentExtracted ConservativeMerge(
        RiskAssessmentExtracted original,
        RiskAssessmentExtracted reExtracted)
    {
        var merged = new RiskAssessmentExtracted();

        // Merge SuicidalIdeation - more severe wins
        merged.SuicidalIdeation = SelectMoreSevereSI(
            original.SuicidalIdeation,
            reExtracted.SuicidalIdeation);

        // Merge SiFrequency - more severe wins
        merged.SiFrequency = SelectMoreSevereSiFreq(
            original.SiFrequency,
            reExtracted.SiFrequency);

        // Merge SiIntensity - more severe wins
        merged.SiIntensity = SelectMoreSevereSiInt(
            original.SiIntensity,
            reExtracted.SiIntensity);

        // Merge SelfHarm - more severe wins
        merged.SelfHarm = SelectMoreSevereSH(
            original.SelfHarm,
            reExtracted.SelfHarm);

        // Merge ShRecency - prefer non-null
        merged.ShRecency = SelectNonNull(original.ShRecency, reExtracted.ShRecency);

        // Merge HomicidalIdeation - more severe wins
        merged.HomicidalIdeation = SelectMoreSevereHI(
            original.HomicidalIdeation,
            reExtracted.HomicidalIdeation);

        // Merge HiTarget - prefer non-null
        merged.HiTarget = SelectNonNull(original.HiTarget, reExtracted.HiTarget);

        // Merge SafetyPlanStatus - prefer reExtracted (more recent assessment)
        merged.SafetyPlanStatus = reExtracted.SafetyPlanStatus.Confidence > 0
            ? reExtracted.SafetyPlanStatus
            : original.SafetyPlanStatus;

        // Merge ProtectiveFactors - combine unique values
        merged.ProtectiveFactors = MergeListFields(
            original.ProtectiveFactors,
            reExtracted.ProtectiveFactors);

        // Merge RiskFactors - combine unique values
        merged.RiskFactors = MergeListFields(
            original.RiskFactors,
            reExtracted.RiskFactors);

        // Merge MeansRestrictionDiscussed - true if either says true
        merged.MeansRestrictionDiscussed = new ExtractedField<bool>
        {
            Value = original.MeansRestrictionDiscussed.Value ||
                    reExtracted.MeansRestrictionDiscussed.Value,
            Confidence = Math.Max(original.MeansRestrictionDiscussed.Confidence,
                                  reExtracted.MeansRestrictionDiscussed.Confidence)
        };

        // Merge RiskLevelOverall - more severe wins
        merged.RiskLevelOverall = SelectMoreSevereRisk(
            original.RiskLevelOverall,
            reExtracted.RiskLevelOverall);

        return merged;
    }

    private static ExtractedField<SuicidalIdeation> SelectMoreSevereSI(
        ExtractedField<SuicidalIdeation> field1,
        ExtractedField<SuicidalIdeation> field2)
    {
        var severity1 = GetSeverityScore("SuicidalIdeation", GetEnumString(field1.Value));
        var severity2 = GetSeverityScore("SuicidalIdeation", GetEnumString(field2.Value));
        return severity1 >= severity2 ? field1 : field2;
    }

    private static ExtractedField<SelfHarm> SelectMoreSevereSH(
        ExtractedField<SelfHarm> field1,
        ExtractedField<SelfHarm> field2)
    {
        var severity1 = GetSeverityScore("SelfHarm", GetEnumString(field1.Value));
        var severity2 = GetSeverityScore("SelfHarm", GetEnumString(field2.Value));
        return severity1 >= severity2 ? field1 : field2;
    }

    private static ExtractedField<HomicidalIdeation> SelectMoreSevereHI(
        ExtractedField<HomicidalIdeation> field1,
        ExtractedField<HomicidalIdeation> field2)
    {
        var severity1 = GetSeverityScore("HomicidalIdeation", GetEnumString(field1.Value));
        var severity2 = GetSeverityScore("HomicidalIdeation", GetEnumString(field2.Value));
        return severity1 >= severity2 ? field1 : field2;
    }

    private static ExtractedField<RiskLevelOverall> SelectMoreSevereRisk(
        ExtractedField<RiskLevelOverall> field1,
        ExtractedField<RiskLevelOverall> field2)
    {
        var severity1 = GetSeverityScore("RiskLevelOverall", GetEnumString(field1.Value));
        var severity2 = GetSeverityScore("RiskLevelOverall", GetEnumString(field2.Value));
        return severity1 >= severity2 ? field1 : field2;
    }

    private static ExtractedField<SiFrequency> SelectMoreSevereSiFreq(
        ExtractedField<SiFrequency> field1,
        ExtractedField<SiFrequency> field2)
    {
        // SiFrequency: Rare=0, Occasional=1, Frequent=2, Constant=3
        var s1 = (int)field1.Value;
        var s2 = (int)field2.Value;
        return s1 >= s2 ? field1 : field2;
    }

    private static ExtractedField<SiIntensity> SelectMoreSevereSiInt(
        ExtractedField<SiIntensity> field1,
        ExtractedField<SiIntensity> field2)
    {
        // SiIntensity: Fleeting=0, Mild=1, Moderate=2, Severe=3
        var s1 = (int)field1.Value;
        var s2 = (int)field2.Value;
        return s1 >= s2 ? field1 : field2;
    }

    private static ExtractedField<string> SelectNonNull(
        ExtractedField<string> field1,
        ExtractedField<string> field2)
    {
        if (!string.IsNullOrEmpty(field1.Value))
            return field1;
        if (!string.IsNullOrEmpty(field2.Value))
            return field2;
        return field1;
    }

    private static ExtractedField<List<string>> MergeListFields(
        ExtractedField<List<string>> field1,
        ExtractedField<List<string>> field2)
    {
        var combined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (field1.Value != null)
        {
            foreach (var item in field1.Value)
                combined.Add(item);
        }

        if (field2.Value != null)
        {
            foreach (var item in field2.Value)
                combined.Add(item);
        }

        return new ExtractedField<List<string>>
        {
            Value = combined.ToList(),
            Confidence = Math.Max(field1.Confidence, field2.Confidence)
        };
    }

    private static void CheckKeywordMismatch(RiskAssessmentResult result, KeywordCheckResult keywords)
    {
        // Check if suicidal keywords found but extraction shows None
        if (keywords.SuicidalMatches.Count > 0)
        {
            var si = result.ValidatedExtraction.SuicidalIdeation.Value;
            if (si == SuicidalIdeation.None)
            {
                result.ReviewReasons.Add(
                    $"Suicidal keywords detected ({string.Join(", ", keywords.SuicidalMatches)}) but extraction shows 'None'");
            }
        }

        // Check if self-harm keywords found but extraction shows None
        if (keywords.SelfHarmMatches.Count > 0)
        {
            var sh = result.ValidatedExtraction.SelfHarm.Value;
            if (sh == SelfHarm.None)
            {
                result.ReviewReasons.Add(
                    $"Self-harm keywords detected ({string.Join(", ", keywords.SelfHarmMatches)}) but extraction shows 'None'");
            }
        }

        // Check if homicidal keywords found but extraction shows None
        if (keywords.HomicidalMatches.Count > 0)
        {
            var hi = result.ValidatedExtraction.HomicidalIdeation.Value;
            if (hi == HomicidalIdeation.None)
            {
                result.ReviewReasons.Add(
                    $"Homicidal keywords detected ({string.Join(", ", keywords.HomicidalMatches)}) but extraction shows 'None'");
            }
        }
    }

    private static void DetermineReviewRequirements(RiskAssessmentResult result)
    {
        // Rule 1: Any discrepancy requires review
        if (result.Discrepancies.Count > 0)
        {
            result.RequiresReview = true;
            if (!result.ReviewReasons.Any(r => r.Contains("Discrepancy", StringComparison.Ordinal)))
            {
                result.ReviewReasons.Add(
                    $"Discrepancies found in {result.Discrepancies.Count} field(s): {string.Join(", ", result.Discrepancies.Select(d => d.FieldName))}");
            }
        }

        // Rule 2: Low confidence on any critical field
        if (HasLowConfidenceRiskField(result.FinalExtraction))
        {
            result.RequiresReview = true;
            result.ReviewReasons.Add("One or more risk fields have confidence below 0.9 threshold");
        }

        // Rule 3: High risk level
        if (result.FinalExtraction.IsHighRisk())
        {
            result.RequiresReview = true;
            result.ReviewReasons.Add("High-risk indicators detected");
        }

        // Rule 4: Re-extraction shows higher risk than original (escalation)
        if (IsEscalation(result.OriginalExtraction, result.ValidatedExtraction))
        {
            result.RequiresReview = true;
            result.ReviewReasons.Add("Re-extraction identified higher risk level than original extraction");
        }

        // Rule 5: Keyword matches already added reasons in CheckKeywordMismatch
        if (result.ReviewReasons.Any(r => r.Contains("keywords detected", StringComparison.Ordinal)))
        {
            result.RequiresReview = true;
        }
    }

    private static bool HasLowConfidenceRiskField(RiskAssessmentExtracted extraction)
    {
        // Check critical fields with non-None values
        if (extraction.SuicidalIdeation.Value != SuicidalIdeation.None &&
            extraction.SuicidalIdeation.Confidence < RiskConfidenceThreshold)
            return true;

        if (extraction.SelfHarm.Value != SelfHarm.None &&
            extraction.SelfHarm.Confidence < RiskConfidenceThreshold)
            return true;

        if (extraction.HomicidalIdeation.Value != HomicidalIdeation.None &&
            extraction.HomicidalIdeation.Confidence < RiskConfidenceThreshold)
            return true;

        if ((extraction.RiskLevelOverall.Value == RiskLevelOverall.High ||
             extraction.RiskLevelOverall.Value == RiskLevelOverall.Imminent) &&
            extraction.RiskLevelOverall.Confidence < RiskConfidenceThreshold)
            return true;

        return false;
    }

    private static bool IsEscalation(
        RiskAssessmentExtracted original,
        RiskAssessmentExtracted reExtracted)
    {
        var originalRisk = GetSeverityScore("RiskLevelOverall",
            GetEnumString(original.RiskLevelOverall.Value));
        var reExtractedRisk = GetSeverityScore("RiskLevelOverall",
            GetEnumString(reExtracted.RiskLevelOverall.Value));

        return reExtractedRisk > originalRisk;
    }

    private static (bool Applied, string? Reason) ApplyHomicidalEvidenceGuardrail(
        RiskAssessmentResult result,
        KeywordCheckResult? keywordResult)
    {
        if (result.FinalExtraction.HomicidalIdeation.Value != HomicidalIdeation.Passive)
        {
            return (false, null);
        }

        var hasHomicidalKeywords = keywordResult?.HomicidalMatches.Count > 0;
        var hasHiTarget = !string.IsNullOrWhiteSpace(result.FinalExtraction.HiTarget.Value);
        if (hasHomicidalKeywords || hasHiTarget)
        {
            return (false, "homicidal_keywords_or_target_present");
        }

        var originalHi = result.OriginalExtraction.HomicidalIdeation;
        var validatedHi = result.ValidatedExtraction.HomicidalIdeation;
        var noneConfidence = Math.Max(originalHi.Confidence, validatedHi.Confidence);

        result.FinalExtraction.HomicidalIdeation = new ExtractedField<HomicidalIdeation>
        {
            Value = HomicidalIdeation.None,
            Confidence = noneConfidence
        };

        return (true, "no_other_directed_homicidal_evidence");
    }

    private static (bool Applied, string? Reason) ApplySelfHarmEvidenceGuardrail(
        RiskAssessmentResult result,
        KeywordCheckResult? keywordResult,
        Dictionary<string, List<string>> criteriaUsed)
    {
        if (result.FinalExtraction.SelfHarm.Value == SelfHarm.None)
        {
            return (false, null);
        }

        var hasSelfHarmKeywords = keywordResult?.SelfHarmMatches.Count > 0;
        if (hasSelfHarmKeywords)
        {
            return (false, "self_harm_keywords_present");
        }

        if (result.ValidatedExtraction.SelfHarm.Value != SelfHarm.None)
        {
            return (false, "reextracted_self_harm_not_none");
        }

        if (!criteriaUsed.TryGetValue("self_harm", out var selfHarmCriteria))
        {
            return (false, "criteria_missing");
        }

        var behaviorAbsent = selfHarmCriteria.Any(criteria =>
            criteria.Equals("self_injury_behavior_absent", StringComparison.OrdinalIgnoreCase) ||
            criteria.Equals("no_self_harm_behavior_reported", StringComparison.OrdinalIgnoreCase));

        if (!behaviorAbsent)
        {
            return (false, "behavior_absence_not_confirmed");
        }

        var originalSh = result.OriginalExtraction.SelfHarm;
        var validatedSh = result.ValidatedExtraction.SelfHarm;
        var noneConfidence = Math.Max(originalSh.Confidence, validatedSh.Confidence);

        result.FinalExtraction.SelfHarm = new ExtractedField<SelfHarm>
        {
            Value = SelfHarm.None,
            Confidence = noneConfidence
        };

        return (true, "no_self_injury_behavior_evidence");
    }

    private static RiskDiagnostics BuildDiagnostics(
        RiskAssessmentExtracted original,
        RiskAssessmentExtracted reExtracted,
        RiskAssessmentExtracted final,
        IReadOnlyCollection<RiskDiscrepancy> discrepancies,
        KeywordCheckResult? keywordResult,
        bool homicidalGuardrailApplied,
        string? homicidalGuardrailReason,
        bool selfHarmGuardrailApplied,
        string? selfHarmGuardrailReason,
        Dictionary<string, List<string>> criteriaUsed,
        Dictionary<string, string> reasoningUsed,
        int criteriaValidationAttemptsUsed)
    {
        var diagnostics = new RiskDiagnostics
        {
            HomicidalGuardrailApplied = homicidalGuardrailApplied,
            HomicidalGuardrailReason = homicidalGuardrailReason,
            HomicidalKeywordMatches = keywordResult?.HomicidalMatches.ToList() ?? [],
            SelfHarmGuardrailApplied = selfHarmGuardrailApplied,
            SelfHarmGuardrailReason = selfHarmGuardrailReason,
            CriteriaValidationAttemptsUsed = Math.Max(1, criteriaValidationAttemptsUsed)
        };

        diagnostics.Decisions.Add(CreateFieldDiagnostic(
            field: "suicidal_ideation",
            originalValue: GetEnumString(original.SuicidalIdeation.Value),
            reExtractedValue: GetEnumString(reExtracted.SuicidalIdeation.Value),
            finalValue: GetEnumString(final.SuicidalIdeation.Value),
            originalSource: original.SuicidalIdeation.Source?.Text,
            reExtractedSource: reExtracted.SuicidalIdeation.Source?.Text,
            finalSource: final.SuicidalIdeation.Source?.Text,
            hadDiscrepancy: discrepancies.Any(d => d.FieldName.Equals(FieldSuicidalIdeation, StringComparison.OrdinalIgnoreCase)),
            guardrailApplied: false,
            criteriaUsed: GetCriteriaForField(criteriaUsed, "suicidal_ideation"),
            reasoningUsed: GetReasoningForField(reasoningUsed, "suicidal_ideation")));

        diagnostics.Decisions.Add(CreateFieldDiagnostic(
            field: "si_frequency",
            originalValue: original.SiFrequency.Value.ToString(),
            reExtractedValue: reExtracted.SiFrequency.Value.ToString(),
            finalValue: final.SiFrequency.Value.ToString(),
            originalSource: original.SiFrequency.Source?.Text,
            reExtractedSource: reExtracted.SiFrequency.Source?.Text,
            finalSource: final.SiFrequency.Source?.Text,
            hadDiscrepancy: false,
            guardrailApplied: false,
            criteriaUsed: GetCriteriaForField(criteriaUsed, "si_frequency"),
            reasoningUsed: GetReasoningForField(reasoningUsed, "si_frequency")));

        diagnostics.Decisions.Add(CreateFieldDiagnostic(
            field: "self_harm",
            originalValue: GetEnumString(original.SelfHarm.Value),
            reExtractedValue: GetEnumString(reExtracted.SelfHarm.Value),
            finalValue: GetEnumString(final.SelfHarm.Value),
            originalSource: original.SelfHarm.Source?.Text,
            reExtractedSource: reExtracted.SelfHarm.Source?.Text,
            finalSource: final.SelfHarm.Source?.Text,
            hadDiscrepancy: discrepancies.Any(d => d.FieldName.Equals(FieldSelfHarm, StringComparison.OrdinalIgnoreCase)),
            guardrailApplied: selfHarmGuardrailApplied,
            criteriaUsed: GetCriteriaForField(criteriaUsed, "self_harm"),
            reasoningUsed: GetReasoningForField(reasoningUsed, "self_harm")));

        diagnostics.Decisions.Add(CreateFieldDiagnostic(
            field: "homicidal_ideation",
            originalValue: GetEnumString(original.HomicidalIdeation.Value),
            reExtractedValue: GetEnumString(reExtracted.HomicidalIdeation.Value),
            finalValue: GetEnumString(final.HomicidalIdeation.Value),
            originalSource: original.HomicidalIdeation.Source?.Text,
            reExtractedSource: reExtracted.HomicidalIdeation.Source?.Text,
            finalSource: final.HomicidalIdeation.Source?.Text,
            hadDiscrepancy: discrepancies.Any(d => d.FieldName.Equals(FieldHomicidalIdeation, StringComparison.OrdinalIgnoreCase)),
            guardrailApplied: homicidalGuardrailApplied,
            criteriaUsed: GetCriteriaForField(criteriaUsed, "homicidal_ideation"),
            reasoningUsed: GetReasoningForField(reasoningUsed, "homicidal_ideation")));

        diagnostics.Decisions.Add(CreateFieldDiagnostic(
            field: "risk_level_overall",
            originalValue: GetEnumString(original.RiskLevelOverall.Value, "Low"),
            reExtractedValue: GetEnumString(reExtracted.RiskLevelOverall.Value, "Low"),
            finalValue: GetEnumString(final.RiskLevelOverall.Value, "Low"),
            originalSource: original.RiskLevelOverall.Source?.Text,
            reExtractedSource: reExtracted.RiskLevelOverall.Source?.Text,
            finalSource: final.RiskLevelOverall.Source?.Text,
            hadDiscrepancy: discrepancies.Any(d => d.FieldName.Equals(FieldRiskLevelOverall, StringComparison.OrdinalIgnoreCase)),
            guardrailApplied: false,
            criteriaUsed: GetCriteriaForField(criteriaUsed, "risk_level_overall"),
            reasoningUsed: GetReasoningForField(reasoningUsed, "risk_level_overall")));

        return diagnostics;
    }

    private static RiskFieldDiagnostic CreateFieldDiagnostic(
        string field,
        string originalValue,
        string reExtractedValue,
        string finalValue,
        string? originalSource,
        string? reExtractedSource,
        string? finalSource,
        bool hadDiscrepancy,
        bool guardrailApplied,
        List<string> criteriaUsed,
        string reasoningUsed)
    {
        var ruleApplied = "no_merge_change";
        if (hadDiscrepancy)
        {
            ruleApplied = "conservative_merge";
        }
        if (guardrailApplied)
        {
            ruleApplied = "guardrail_override";
        }

        return new RiskFieldDiagnostic
        {
            Field = field,
            OriginalValue = originalValue,
            ReExtractedValue = reExtractedValue,
            FinalValue = finalValue,
            RuleApplied = ruleApplied,
            OriginalSource = NormalizeSource(originalSource),
            ReExtractedSource = NormalizeSource(reExtractedSource),
            FinalSource = NormalizeSource(finalSource),
            CriteriaUsed = criteriaUsed,
            ReasoningUsed = NormalizeReasoning(reasoningUsed)
        };
    }

    private static List<string> GetCriteriaForField(
        Dictionary<string, List<string>> criteriaUsed,
        string fieldName)
    {
        if (criteriaUsed.TryGetValue(fieldName, out var criteria) && criteria.Count > 0)
        {
            return criteria;
        }

        return [];
    }

    private static string GetReasoningForField(
        Dictionary<string, string> reasoningUsed,
        string fieldName)
    {
        if (reasoningUsed.TryGetValue(fieldName, out var reasoning) &&
            !string.IsNullOrWhiteSpace(reasoning))
        {
            return NormalizeReasoning(reasoning);
        }

        return string.Empty;
    }

    private static string? NormalizeSource(string? sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return null;
        }

        var trimmed = sourceText.Trim();
        return trimmed.Length <= 220
            ? trimmed
            : trimmed[..220];
    }

    private static string NormalizeReasoning(string? reasoningText)
    {
        if (string.IsNullOrWhiteSpace(reasoningText))
        {
            return string.Empty;
        }

        var trimmed = reasoningText.Trim();
        return trimmed.Length <= 320
            ? trimmed
            : trimmed[..320];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting risk assessment for session {SessionId}")]
    private static partial void LogStartingRiskAssessment(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during risk re-extraction for session {SessionId}")]
    private static partial void LogRiskReExtractionError(ILogger logger, Exception exception, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Risk assessment completed for session {SessionId}. RequiresReview: {RequiresReview}, RiskLevel: {RiskLevel}, Discrepancies: {DiscrepancyCount}")]
    private static partial void LogRiskAssessmentCompleted(ILogger logger, string sessionId, bool requiresReview, RiskLevelOverall? riskLevel, int discrepancyCount);

    private sealed class MissingDiagnosticFeedbackException : InvalidOperationException
    {
        public MissingDiagnosticFeedbackException(
            IReadOnlyCollection<string> missingCriteriaKeys,
            IReadOnlyCollection<string> missingReasoningKeys)
            : base(
                $"Missing required diagnostic feedback. criteria_used: [{string.Join(", ", missingCriteriaKeys)}], reasoning_used: [{string.Join(", ", missingReasoningKeys)}]")
        {
        }
    }
}
