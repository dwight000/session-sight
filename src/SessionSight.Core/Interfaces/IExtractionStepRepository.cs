using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface IExtractionStepRepository
{
    Task SaveStepAsync(ExtractionStep extractionStep, CancellationToken ct = default);
    Task<IReadOnlyList<ExtractionStep>> GetStepsByExtractionIdAsync(Guid extractionId, CancellationToken ct = default);
}
