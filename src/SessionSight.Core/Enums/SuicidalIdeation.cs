using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuicidalIdeation
{
    None,
    Passive,
    ActiveNoPlan,
    ActiveWithPlan,
    ActiveWithIntent
}
