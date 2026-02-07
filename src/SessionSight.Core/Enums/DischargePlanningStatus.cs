using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DischargePlanningStatus
{
    NotPlanned,
    InProgress,
    ReadyForDischarge,
    Discharged,
    NotApplicable
}
