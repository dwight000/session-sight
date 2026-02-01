using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
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
public class RiskAssessorAgent : IRiskAssessorAgent
{
    private readonly IAIFoundryClientFactory _clientFactory;
    private readonly IModelRouter _modelRouter;
    private readonly RiskAssessorOptions _options;
    private readonly ILogger<RiskAssessorAgent> _logger;

    private const double RiskConfidenceThreshold = 0.9;

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
        _logger.LogInformation("Starting risk assessment for session {SessionId}", extraction.SessionId);

        var result = new RiskAssessmentResult
        {
            OriginalExtraction = extraction.Data.RiskAssessment
        };

        // Step 1: Re-extract with focused safety prompt
        var modelName = _modelRouter.SelectModel(ModelTask.RiskAssessment);
        result.ModelUsed = modelName;

        try
        {
            var reExtracted = await ReExtractRiskAsync(originalNoteText, modelName, ct);
            result.ValidatedExtraction = reExtracted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during risk re-extraction for session {SessionId}", extraction.SessionId);
            result.ValidatedExtraction = new RiskAssessmentExtracted();
            result.ReviewReasons.Add($"Re-extraction failed: {ex.Message}");
            result.RequiresReview = true;
        }

        // Step 2: Check for danger keywords (safety net)
        if (_options.EnableKeywordSafetyNet)
        {
            var keywordResult = DangerKeywordChecker.Check(originalNoteText);
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

        result.DeterminedRiskLevel = result.FinalExtraction.RiskLevelOverall.Value;

        // Step 5: Determine if review is required
        DetermineReviewRequirements(result);

        _logger.LogInformation(
            "Risk assessment completed for session {SessionId}. RequiresReview: {RequiresReview}, RiskLevel: {RiskLevel}, Discrepancies: {DiscrepancyCount}",
            extraction.SessionId, result.RequiresReview, result.DeterminedRiskLevel, result.Discrepancies.Count);

        return result;
    }

    private async Task<RiskAssessmentExtracted> ReExtractRiskAsync(
        string noteText,
        string modelName,
        CancellationToken ct)
    {
        var chatClient = _clientFactory.CreateChatClient(modelName);
        var prompt = RiskPrompts.GetRiskReExtractionPrompt(noteText);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(RiskPrompts.SystemPrompt),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.1f,
            MaxOutputTokenCount = 2048
        };

        var response = await chatClient.CompleteChatAsync(messages, options, ct);
        var content = response.Value.Content[0].Text;

        return ParseRiskResponse(content) ?? new RiskAssessmentExtracted();
    }

    internal static RiskAssessmentExtracted? ParseRiskResponse(string content)
    {
        var json = ExtractJson(content);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
            if (parsed == null)
            {
                return null;
            }

            return MapToRiskAssessment(parsed);
        }
        catch (JsonException)
        {
            return null;
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

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
        {
            var stringValue = element.GetString();
            if (string.IsNullOrEmpty(stringValue))
                return null;
            return Enum.TryParse(underlyingType, stringValue, ignoreCase: true, out var result) ? result : null;
        }

        if (underlyingType == typeof(string))
            return element.GetString();

        if (underlyingType == typeof(bool))
            return element.ValueKind == JsonValueKind.True;

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

    internal static List<RiskDiscrepancy> FindDiscrepancies(
        RiskAssessmentExtracted original,
        RiskAssessmentExtracted reExtracted)
    {
        var discrepancies = new List<RiskDiscrepancy>();

        CheckDiscrepancy(discrepancies, "SuicidalIdeation",
            GetEnumString(original.SuicidalIdeation.Value),
            original.SuicidalIdeation.Confidence,
            GetEnumString(reExtracted.SuicidalIdeation.Value),
            reExtracted.SuicidalIdeation.Confidence);

        CheckDiscrepancy(discrepancies, "SelfHarm",
            GetEnumString(original.SelfHarm.Value),
            original.SelfHarm.Confidence,
            GetEnumString(reExtracted.SelfHarm.Value),
            reExtracted.SelfHarm.Confidence);

        CheckDiscrepancy(discrepancies, "HomicidalIdeation",
            GetEnumString(original.HomicidalIdeation.Value),
            original.HomicidalIdeation.Confidence,
            GetEnumString(reExtracted.HomicidalIdeation.Value),
            reExtracted.HomicidalIdeation.Confidence);

        CheckDiscrepancy(discrepancies, "RiskLevelOverall",
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
            "SuicidalIdeation" => value switch
            {
                "None" => 0,
                "Passive" => 1,
                "ActiveNoPlan" => 2,
                "ActiveWithPlan" => 3,
                "ActiveWithIntent" => 4,
                _ => 0
            },
            "SelfHarm" => value switch
            {
                "None" => 0,
                "Historical" => 1,
                "Recent" => 2,
                "Current" => 3,
                "Imminent" => 4,
                _ => 0
            },
            "HomicidalIdeation" => value switch
            {
                "None" => 0,
                "Passive" => 1,
                "ActiveNoPlan" => 2,
                "ActiveWithPlan" => 3,
                _ => 0
            },
            "RiskLevelOverall" => value switch
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
            Value = (original.MeansRestrictionDiscussed.Value == true) ||
                    (reExtracted.MeansRestrictionDiscussed.Value == true),
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
        SiFrequency? v1 = field1.Value;
        SiFrequency? v2 = field2.Value;
        var s1 = v1.HasValue ? (int)v1.Value : 0;
        var s2 = v2.HasValue ? (int)v2.Value : 0;
        return s1 >= s2 ? field1 : field2;
    }

    private static ExtractedField<SiIntensity> SelectMoreSevereSiInt(
        ExtractedField<SiIntensity> field1,
        ExtractedField<SiIntensity> field2)
    {
        // SiIntensity: Fleeting=0, Mild=1, Moderate=2, Severe=3
        SiIntensity? v1 = field1.Value;
        SiIntensity? v2 = field2.Value;
        var s1 = v1.HasValue ? (int)v1.Value : 0;
        var s2 = v2.HasValue ? (int)v2.Value : 0;
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

    private void CheckKeywordMismatch(RiskAssessmentResult result, KeywordCheckResult keywords)
    {
        // Check if suicidal keywords found but extraction shows None
        if (keywords.SuicidalMatches.Count > 0)
        {
            SuicidalIdeation? si = result.ValidatedExtraction.SuicidalIdeation.Value;
            if (!si.HasValue || si.Value == SuicidalIdeation.None)
            {
                result.ReviewReasons.Add(
                    $"Suicidal keywords detected ({string.Join(", ", keywords.SuicidalMatches)}) but extraction shows 'None'");
            }
        }

        // Check if self-harm keywords found but extraction shows None
        if (keywords.SelfHarmMatches.Count > 0)
        {
            SelfHarm? sh = result.ValidatedExtraction.SelfHarm.Value;
            if (!sh.HasValue || sh.Value == SelfHarm.None)
            {
                result.ReviewReasons.Add(
                    $"Self-harm keywords detected ({string.Join(", ", keywords.SelfHarmMatches)}) but extraction shows 'None'");
            }
        }

        // Check if homicidal keywords found but extraction shows None
        if (keywords.HomicidalMatches.Count > 0)
        {
            HomicidalIdeation? hi = result.ValidatedExtraction.HomicidalIdeation.Value;
            if (!hi.HasValue || hi.Value == HomicidalIdeation.None)
            {
                result.ReviewReasons.Add(
                    $"Homicidal keywords detected ({string.Join(", ", keywords.HomicidalMatches)}) but extraction shows 'None'");
            }
        }
    }

    private void DetermineReviewRequirements(RiskAssessmentResult result)
    {
        // Rule 1: Any discrepancy requires review
        if (result.Discrepancies.Count > 0)
        {
            result.RequiresReview = true;
            if (!result.ReviewReasons.Any(r => r.Contains("Discrepancy")))
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
        if (result.ReviewReasons.Any(r => r.Contains("keywords detected")))
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
}
