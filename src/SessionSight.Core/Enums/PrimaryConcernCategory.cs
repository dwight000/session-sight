using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PrimaryConcernCategory
{
    Anxiety,
    Depression,
    Trauma,
    Relationship,
    Grief,
    SubstanceUse,
    Eating,
    Sleep,
    Anger,
    SelfEsteem,
    WorkStress,
    LifeTransition,
    Other
}
