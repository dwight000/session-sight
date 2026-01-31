using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JudgmentLevel
{
    Good,
    Fair,
    Poor,
    Impaired
}
