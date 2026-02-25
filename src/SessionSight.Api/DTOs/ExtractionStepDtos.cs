namespace SessionSight.Api.DTOs;

public record ExtractionStepsResponseDto(
    Guid ExtractionId,
    string? DocumentStatus,
    string? FailureKind,
    string? ErrorMessage,
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
    IReadOnlyList<ExtractionToolCallDto> ToolCalls,
    IReadOnlyList<ExtractionLlmTraceDto> LlmTraces);

public record ExtractionToolCallDto(
    string ToolName,
    int LoopRound,
    bool Succeeded,
    long DurationMs,
    DateTime CalledAt,
    string? InputJson,
    string? OutputJson);

public record ExtractionLlmTraceDto(
    string ModelUsed,
    int LoopRound,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    long DurationMs,
    string? PromptText,
    string? ResponseText,
    DateTime CalledAt);
