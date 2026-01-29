using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnergyLevel
{
    Low,
    Normal,
    Elevated,
    Fluctuating
}
