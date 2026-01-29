using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HomicidalIdeation
{
    None,
    Passive,
    ActiveNoPlan,
    ActiveWithPlan
}
