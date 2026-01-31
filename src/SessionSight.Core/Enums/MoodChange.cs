using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MoodChange
{
    SignificantlyImproved,
    Improved,
    Stable,
    Declined,
    SignificantlyDeclined,
    Unknown
}
