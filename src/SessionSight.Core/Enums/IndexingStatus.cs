using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IndexingStatus
{
    None,
    Indexed,
    Failed
}
