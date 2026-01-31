using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LevelOfCareRecommendation
{
    Outpatient,
    IntensiveOutpatient,
    PartialHospitalization,
    Inpatient,
    Residential
}
