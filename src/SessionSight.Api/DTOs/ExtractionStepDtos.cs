namespace SessionSight.Api.DTOs;

public record ExtractionStepsResponseDto(
    Guid ExtractionId,
    IReadOnlyList<ExtractionStepDto> Steps);

public record ExtractionStepDto(
    Guid Id,
    string StepName,
    string Status,
    int StepOrder,
    DateTime StartedAt,
    DateTime? CompletedAt,
    long DurationMs,
    string ModelUsed,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    string? ResultSummaryJson,
    string? ErrorMessage,
    IReadOnlyList<ExtractionToolCallDto> ToolCalls);

public record ExtractionToolCallDto(
    string ToolName,
    int LoopRound,
    bool Succeeded,
    long DurationMs,
    DateTime CalledAt);
