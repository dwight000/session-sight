using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HomeworkCompletion
{
    Completed,
    PartiallyCompleted,
    NotCompleted,
    NotAssigned
}
