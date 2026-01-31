using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InsightLevel
{
    Good,
    Fair,
    Poor,
    Absent
}
