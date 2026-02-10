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
    ClinicalExtraction Data,
    RiskDiagnosticsDto? RiskDiagnostics);

public record RiskDiagnosticsDto(
    bool GuardrailApplied,
    GuardrailDetailDto? HomicidalGuardrail,
    GuardrailDetailDto? SelfHarmGuardrail,
    int CriteriaValidationAttempts,
    int DiscrepancyCount,
    List<RiskFieldDecisionDto> FieldDecisions);

public record GuardrailDetailDto(bool Applied, string? Reason);

public record RiskFieldDecisionDto(
    string Field,
    string OriginalValue,
    string ReExtractedValue,
    string FinalValue,
    string RuleApplied,
    List<string> CriteriaUsed,
    string ReasoningUsed);
