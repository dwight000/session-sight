using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface IExtractionStepRepository
{
    Task SaveStepAsync(ExtractionStep extractionStep, CancellationToken ct = default);
    Task<IReadOnlyList<ExtractionStep>> GetStepsByExtractionIdAsync(Guid extractionId, CancellationToken ct = default);
    Task SaveLlmTraceAsync(ExtractionLlmTrace trace, CancellationToken ct = default);
    Task SaveToolCallsAsync(IEnumerable<ExtractionToolCall> toolCalls, CancellationToken ct = default);
}
