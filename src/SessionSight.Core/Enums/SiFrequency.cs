using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SiFrequency
{
    Rare,
    Occasional,
    Frequent,
    Constant
}
