using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionType
{
    Intake,
    Individual,
    Group,
    Family,
    Couples,
    Crisis,
    Assessment,
    Termination
}
