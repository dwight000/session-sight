using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ObservedAffect
{
    Bright,
    Euthymic,
    Flat,
    Blunted,
    Tearful,
    Anxious,
    Agitated,
    Irritable,
    Labile,
    Incongruent
}
