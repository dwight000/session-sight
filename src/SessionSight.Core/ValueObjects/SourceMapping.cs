using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionSight.Core.ValueObjects;

[JsonConverter(typeof(SourceMappingConverter))]
public class SourceMapping
{
    public string Text { get; set; } = string.Empty;
    public int StartChar { get; set; }
    public int EndChar { get; set; }
    public string? Section { get; set; }
}

/// <summary>
/// Handles both object form {"text": "...", "section": "..."} and
/// plain string form "source text" from LLM responses.
/// </summary>
internal sealed class SourceMappingConverter : JsonConverter<SourceMapping>
{
#pragma warning disable S3776 // Cognitive complexity - JSON parsing requires branching for token types
    public override SourceMapping? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
#pragma warning restore S3776
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            return new SourceMapping { Text = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var mapping = new SourceMapping();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return mapping;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var prop = reader.GetString();
                reader.Read();

                switch (prop?.ToLowerInvariant())
                {
                    case "text":
                        mapping.Text = reader.GetString() ?? string.Empty;
                        break;
                    case "startchar":
                        if (reader.TokenType == JsonTokenType.Number) mapping.StartChar = reader.GetInt32();
                        break;
                    case "endchar":
                        if (reader.TokenType == JsonTokenType.Number) mapping.EndChar = reader.GetInt32();
                        break;
                    case "section":
                        mapping.Section = reader.GetString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return mapping;
        }

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, SourceMapping value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("text", value.Text);
        writer.WriteNumber("startChar", value.StartChar);
        writer.WriteNumber("endChar", value.EndChar);
        writer.WriteString("section", value.Section);
        writer.WriteEndObject();
    }
}
