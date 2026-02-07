using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Appearance
{
    WellGroomed,
    Appropriate,
    Disheveled,
    Unkempt,
    Bizarre,
    Unremarkable
}
