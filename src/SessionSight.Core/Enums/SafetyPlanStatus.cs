using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SafetyPlanStatus
{
    NotNeeded,
    InPlace,
    NeedsUpdate,
    NeedsCreation,
    Declined
}
