using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpeechType
{
    Normal,
    Pressured,
    Slowed,
    Soft,
    Loud,
    Monotone
}
