using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagnosisChangeType
{
    New,
    Updated,
    Removed,
    NoChange,
    Deferred
}
