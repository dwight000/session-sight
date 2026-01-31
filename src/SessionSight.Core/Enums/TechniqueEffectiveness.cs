using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TechniqueEffectiveness
{
    VeryEffective,
    Effective,
    SomewhatEffective,
    NotEffective,
    UnableToAssess
}
