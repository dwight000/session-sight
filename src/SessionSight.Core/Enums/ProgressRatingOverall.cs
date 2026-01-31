using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProgressRatingOverall
{
    SignificantImprovement,
    SomeImprovement,
    Stable,
    SomeRegression,
    SignificantRegression
}
