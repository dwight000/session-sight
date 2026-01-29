using SessionSight.Core.Schema;

namespace SessionSight.Api.DTOs;

public record ExtractionResultDto(
    Guid Id,
    Guid SessionId,
    string SchemaVersion,
    string ModelUsed,
    double OverallConfidence,
    bool RequiresReview,
    DateTime ExtractedAt,
    ClinicalExtraction Data);
