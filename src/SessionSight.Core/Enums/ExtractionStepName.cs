using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExtractionStepName
{
    DocumentParse,
    Intake,
    ClinicalExtract,
    RiskAssess,
    Summarize,
    SearchIndex
}
