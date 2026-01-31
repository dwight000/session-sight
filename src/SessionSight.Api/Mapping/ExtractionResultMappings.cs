using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;

namespace SessionSight.Api.Mapping;

public static class ExtractionResultMappings
{
    public static ExtractionResultDto ToDto(this ExtractionResult result) =>
        new(result.Id, result.SessionId, result.SchemaVersion,
            result.ModelUsed, result.OverallConfidence, result.RequiresReview,
            result.ExtractedAt, result.Data);
}
