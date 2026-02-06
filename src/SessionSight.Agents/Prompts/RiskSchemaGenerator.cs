using System.Globalization;
using System.Text;
using SessionSight.Core.Schema;

namespace SessionSight.Agents.Prompts;

/// <summary>
/// Generates the JSON schema string for RiskAssessmentExtracted from C# types via reflection.
/// Same pattern as ExtractionSchemaGenerator but scoped to risk fields only.
/// </summary>
internal static class RiskSchemaGenerator
{
    private static string? _cached;

    /// <summary>
    /// Returns a compact JSON schema showing all risk field names, value types, and enum values.
    /// Cached after first call.
    /// </summary>
    public static string Generate()
    {
        return _cached ??= BuildSchema();
    }

    /// <summary>
    /// Clears the cached schema. For testing only.
    /// </summary>
    internal static void ClearCache() => _cached = null;

    private static string BuildSchema()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        var fields = typeof(RiskAssessmentExtracted).GetProperties();
        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var fieldName = ToCamelCase(field.Name);
            var valueType = GetValueTypeDescription(field.PropertyType);
            var comma = i < fields.Length - 1 ? "," : "";
            sb.Append(CultureInfo.InvariantCulture,
                $"  \"{fieldName}\": {{ \"value\": {valueType}, \"confidence\": 0.0, \"source\": null }}{comma}\n");
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
        if (underlying == typeof(bool))
            return "false";

        if (underlying.IsEnum)
        {
            var values = string.Join("|", Enum.GetNames(underlying));
            return $"\"{values}\"";
        }

        if (underlying == typeof(List<string>))
            return "[\"string\"]";

        return "null";
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
