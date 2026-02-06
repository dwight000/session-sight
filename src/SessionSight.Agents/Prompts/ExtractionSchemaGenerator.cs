using System.Globalization;
using System.Text;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Prompts;

/// <summary>
/// Generates the JSON schema string for ClinicalExtraction from C# types via reflection.
/// Used in the agent loop system prompt so the LLM knows exact field names and types.
/// </summary>
internal static class ExtractionSchemaGenerator
{
    private static string? _cached;

    /// <summary>
    /// Returns a compact JSON schema showing all section/field names, value types, and enum values.
    /// Cached after first call.
    /// </summary>
    public static string Generate()
    {
        return _cached ??= BuildSchema();
    }

    private static string BuildSchema()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        var sectionProps = typeof(ClinicalExtraction).GetProperties()
            .Where(p => p.Name != "Metadata")
            .ToArray();

        for (var i = 0; i < sectionProps.Length; i++)
        {
            var section = sectionProps[i];
            var sectionName = ToCamelCase(section.Name);
            sb.Append(CultureInfo.InvariantCulture, $"  \"{sectionName}\": {{\n");

            var fields = section.PropertyType.GetProperties();
            for (var j = 0; j < fields.Length; j++)
            {
                var field = fields[j];
                var fieldName = ToCamelCase(field.Name);

                // ExtractedField<T> — get T
                var valueType = GetValueTypeDescription(field.PropertyType);
                var comma = j < fields.Length - 1 ? "," : "";
                sb.Append(CultureInfo.InvariantCulture, $"    \"{fieldName}\": {{ \"value\": {valueType}, \"confidence\": 0.0, \"source\": null }}{comma}\n");
            }

            var sectionComma = i < sectionProps.Length - 1 ? "," : "";
            sb.Append(CultureInfo.InvariantCulture, $"  }}{sectionComma}\n");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string GetValueTypeDescription(Type fieldType)
    {
        if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(ExtractedField<>))
            return "null";

        var innerType = fieldType.GetGenericArguments()[0];
        var underlying = Nullable.GetUnderlyingType(innerType) ?? innerType;

        if (underlying == typeof(string))
            return "\"string\"";
        if (underlying == typeof(int))
            return "0";
        if (underlying == typeof(double))
            return "0.0";
        if (underlying == typeof(bool))
            return "false";
        if (underlying == typeof(DateOnly))
            return "\"YYYY-MM-DD\"";
        if (underlying == typeof(TimeOnly))
            return "\"HH:MM\"";

        if (underlying.IsEnum)
        {
            var values = string.Join("|", Enum.GetNames(underlying));
            return $"\"{values}\"";
        }

        if (underlying == typeof(List<string>))
            return "[\"string\"]";

        if (underlying == typeof(Dictionary<string, string>))
            return "{{\"key\": \"value\"}}";

        // List<TEnum>
        if (underlying.IsGenericType &&
            underlying.GetGenericTypeDefinition() == typeof(List<>) &&
            underlying.GetGenericArguments()[0].IsEnum)
        {
            var enumType = underlying.GetGenericArguments()[0];
            var values = string.Join("|", Enum.GetNames(enumType));
            return $"[\"{values}\"]";
        }

        return "null";
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
