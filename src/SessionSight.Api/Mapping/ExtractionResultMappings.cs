using System.Text.Json;
using SessionSight.Api.DTOs;
using SessionSight.Core.Entities;

namespace SessionSight.Api.Mapping;

public static class ExtractionResultMappings
{
    public static ExtractionStepsResponseDto ToStepsDto(
        this IReadOnlyList<ExtractionStep> steps,
        Guid extractionId,
        string? documentStatus = null) =>
        new(extractionId, documentStatus, steps.Select(s => new ExtractionStepDto(
            s.Id,
            s.StepName.ToString(),
            s.Status.ToString(),
            s.StepOrder,
            s.StartedAt,
            s.CompletedAt,
            s.DurationMs,
            s.ModelUsed,
            s.InputTokens,
            s.OutputTokens,
            s.TotalTokens,
            s.ResultSummaryJson,
            s.ErrorMessage,
            s.ToolCalls.OrderBy(tc => tc.LoopRound).ThenBy(tc => tc.CalledAt)
                .Select(tc => new ExtractionToolCallDto(
                tc.ToolName,
                tc.LoopRound,
                tc.Succeeded,
                tc.DurationMs,
                tc.CalledAt,
                tc.InputJson,
                tc.OutputJson
            )).ToList(),
            s.LlmTraces.OrderBy(lt => lt.LoopRound).ThenBy(lt => lt.CalledAt)
                .Select(lt => new ExtractionLlmTraceDto(
                lt.ModelUsed,
                lt.LoopRound,
                lt.InputTokens,
                lt.OutputTokens,
                lt.TotalTokens,
                lt.DurationMs,
                lt.PromptText,
                lt.ResponseText,
                lt.CalledAt
            )).ToList()
        )).ToList());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ExtractionResultDto ToDto(this ExtractionResult result) =>
        new(result.Id, result.SessionId, result.SchemaVersion,
            result.ModelUsed, result.OverallConfidence, result.RequiresReview,
            result.ExtractedAt, result.Data,
            BuildRiskDiagnostics(result));

    private static RiskDiagnosticsDto? BuildRiskDiagnostics(ExtractionResult result)
    {
        // If no diagnostics data exists at all, return null
        if (!result.GuardrailApplied
            && !result.HomicidalGuardrailApplied
            && !result.SelfHarmGuardrailApplied
            && result.CriteriaValidationAttempts <= 1
            && result.DiscrepancyCount == 0
            && result.RiskFieldDecisionsJson is null)
        {
            return null;
        }

        var fieldDecisions = DeserializeFieldDecisions(result.RiskFieldDecisionsJson);

        return new RiskDiagnosticsDto(
            result.GuardrailApplied,
            result.HomicidalGuardrailApplied
                ? new GuardrailDetailDto(true, result.HomicidalGuardrailReason)
                : null,
            result.SelfHarmGuardrailApplied
                ? new GuardrailDetailDto(true, result.SelfHarmGuardrailReason)
                : null,
            result.CriteriaValidationAttempts,
            result.DiscrepancyCount,
            fieldDecisions);
    }

    private static List<RiskFieldDecisionDto> DeserializeFieldDecisions(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<RiskFieldDecisionDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
