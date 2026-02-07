using System.Text.Json.Serialization;

namespace SessionSight.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewStatus { NotFlagged, Pending, Approved, Dismissed }
