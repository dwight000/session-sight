using System.Text.Json;
using SessionSight.Core.Schema;
using SessionSight.Core.ValueObjects;

namespace SessionSight.Agents;

/// <summary>
/// Lenient parser for LLM-generated clinical extraction JSON.
/// Handles type mismatches (e.g., null for value types like TimeOnly)
/// that <see cref="JsonSerializer"/> cannot deserialize directly.
/// Also handles both camelCase (LLM) and PascalCase (C# serializer) property names.
/// </summary>
internal static class LlmExtractionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parses a single section from LLM-generated JSON into a strongly-typed object.
    /// Used by <see cref="Agents.ClinicalExtractorAgent.ParseSectionResponse{T}"/>.
    /// </summary>
    public static T ParseSection<T>(string json) where T : new()
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
            return parsed is null ? new T() : MapToSection<T>(parsed);
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    /// <summary>
    /// Parses an LLM-generated JSON string into a <see cref="ClinicalExtraction"/>.
    /// Tries strict deserialization first, falls back to lenient field-by-field parsing.
    /// </summary>
    public static ClinicalExtraction? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        // Try strict deserialization first
        try
        {
            return JsonSerializer.Deserialize<ClinicalExtraction>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Fall through to lenient parsing
        }

        // Lenient: parse as JsonDocument and map sections individually
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseFromElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a <see cref="JsonElement"/> into a <see cref="ClinicalExtraction"/>.
    /// Used by tools that receive LLM JSON as a <see cref="JsonElement"/>.
    /// </summary>
    public static ClinicalExtraction? ParseFromElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var extraction = new ClinicalExtraction();

        extraction.SessionInfo = TryParseSection<SessionInfoExtracted>(root, "sessionInfo");
        extraction.PresentingConcerns = TryParseSection<PresentingConcernsExtracted>(root, "presentingConcerns");
        extraction.MoodAssessment = TryParseSection<MoodAssessmentExtracted>(root, "moodAssessment");
        extraction.RiskAssessment = TryParseSection<RiskAssessmentExtracted>(root, "riskAssessment");
        extraction.MentalStatusExam = TryParseSection<MentalStatusExamExtracted>(root, "mentalStatusExam");
        extraction.Interventions = TryParseSection<InterventionsExtracted>(root, "interventions");
        extraction.Diagnoses = TryParseSection<DiagnosesExtracted>(root, "diagnoses");
        extraction.TreatmentProgress = TryParseSection<TreatmentProgressExtracted>(root, "treatmentProgress");
        extraction.NextSteps = TryParseSection<NextStepsExtracted>(root, "nextSteps");

        return extraction;
    }

    /// <summary>
    /// Case-insensitive property lookup on JsonElement.
    /// Tries exact match first, then swaps first-char case (camelCase ↔ PascalCase).
    /// </summary>
    private static bool TryGetProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;
        var alt = char.IsLower(name[0])
            ? char.ToUpperInvariant(name[0]) + name[1..]
            : char.ToLowerInvariant(name[0]) + name[1..];
        return element.TryGetProperty(alt, out value);
    }

    private static T TryParseSection<T>(JsonElement root, string sectionName) where T : new()
    {
        if (!TryGetProp(root, sectionName, out var sectionElement) ||
            sectionElement.ValueKind != JsonValueKind.Object)
        {
            return new T();
        }

        try
        {
            var sectionJson = sectionElement.GetRawText();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sectionJson, JsonOptions);
            return parsed is null ? new T() : MapToSection<T>(parsed);
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    private static T MapToSection<T>(Dictionary<string, JsonElement> parsed) where T : new()
    {
        var section = new T();
        var properties = typeof(T).GetProperties();

        // Build case-insensitive lookup for JSON keys
        var ciLookup = new Dictionary<string, JsonElement>(parsed.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in parsed)
            ciLookup.TryAdd(kv.Key, kv.Value);

        foreach (var prop in properties)
        {
            if (!ciLookup.TryGetValue(prop.Name, out var element))
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

        if (TryGetProp(element, "value", out var valueElement))
        {
            var value = DeserializeValue(valueElement, valueType);
            valueProperty?.SetValue(field, value);
        }

        if (TryGetProp(element, "confidence", out var confElement))
        {
            double? conf = confElement.ValueKind switch
            {
                JsonValueKind.Number => confElement.GetDouble(),
                JsonValueKind.String when double.TryParse(confElement.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
                _ => null
            };
            if (conf.HasValue)
                confidenceProperty?.SetValue(field, conf.Value);
        }

        if (TryGetProp(element, "source", out var sourceElement) && sourceElement.ValueKind != JsonValueKind.Null)
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

        return underlyingType switch
        {
            _ when underlyingType.IsEnum => DeserializeEnum(element, underlyingType),
            _ when underlyingType == typeof(string) => element.GetString(),
            _ when underlyingType == typeof(int) => TryGetInt(element),
            _ when underlyingType == typeof(bool) => element.ValueKind == JsonValueKind.True,
            _ when underlyingType == typeof(double) => TryGetDouble(element),
            _ when underlyingType == typeof(DateOnly) => DeserializeDateOnly(element),
            _ when underlyingType == typeof(TimeOnly) => DeserializeTimeOnly(element),
            _ when underlyingType == typeof(List<string>) => DeserializeStringList(element),
            _ when underlyingType == typeof(Dictionary<string, string>) => DeserializeStringDictionary(element),
            _ when IsEnumList(underlyingType) => DeserializeEnumList(element, underlyingType),
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out var i)) return i;
            if (element.TryGetDouble(out var d)) return (int)d;
            return null;
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static double? TryGetDouble(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetDouble(out var d) ? d : null;

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static object? DeserializeEnum(JsonElement element, Type enumType)
    {
        // Handle string enum values (from LLM: "ActiveNoPlan")
        if (element.ValueKind == JsonValueKind.String)
        {
            var stringValue = element.GetString();
            if (string.IsNullOrEmpty(stringValue))
                return null;
            return Enum.TryParse(enumType, stringValue, ignoreCase: true, out var result) ? result : null;
        }

        // Handle numeric enum values (from C# serializer: 2)
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intValue))
        {
            return Enum.IsDefined(enumType, intValue) ? Enum.ToObject(enumType, intValue) : null;
        }

        return null;
    }

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "M/d/yyyy",
        "MM/dd/yyyy",
        "MMMM d, yyyy",
        "MMM d, yyyy",
        "d MMMM yyyy",
        "yyyy/MM/dd"
    ];

    private static DateOnly? DeserializeDateOnly(JsonElement element)
    {
        var dateStr = element.GetString();
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        if (DateOnly.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            return date;

        if (DateOnly.TryParseExact(dateStr, DateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
            return date;

        return null;
    }

    private static TimeOnly? DeserializeTimeOnly(JsonElement element)
    {
        var timeStr = element.GetString();
        return TimeOnly.TryParse(timeStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time) ? time : null;
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

    private static Dictionary<string, string> DeserializeStringDictionary(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return [];

        return element.EnumerateObject()
            .Where(prop => prop.Value.GetString() != null)
            .ToDictionary(prop => prop.Name, prop => prop.Value.GetString()!);
    }

    private static bool IsEnumList(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(List<>) &&
        type.GetGenericArguments()[0].IsEnum;

    private static object? DeserializeEnumList(JsonElement element, Type listType)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return null;

        var itemType = listType.GetGenericArguments()[0];
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

        foreach (var item in element.EnumerateArray())
        {
            var parsed = DeserializeEnum(item, itemType);
            if (parsed != null)
                list.Add(parsed);
        }

        return list;
    }

    private static SourceMapping? DeserializeSourceMapping(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return new SourceMapping { Text = element.GetString() ?? string.Empty };

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var mapping = new SourceMapping();

        if (TryGetProp(element, "text", out var textElement))
            mapping.Text = textElement.GetString() ?? string.Empty;

        if (TryGetProp(element, "startChar", out var startElement) && startElement.TryGetInt32(out var start))
            mapping.StartChar = start;

        if (TryGetProp(element, "endChar", out var endElement) && endElement.TryGetInt32(out var end))
            mapping.EndChar = end;

        if (TryGetProp(element, "section", out var sectionElement))
            mapping.Section = sectionElement.GetString();

        return mapping;
    }
}
