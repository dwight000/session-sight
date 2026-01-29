using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThoughtProcess
{
    Linear,
    Circumstantial,
    Tangential,
    Loose,
    FlightOfIdeas,
    Blocking
}
