using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SelfHarm
{
    None,
    Historical,
    Recent,
    Current,
    Imminent
}
