using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface IExtractionResultRepository
{
    Task<ExtractionResult?> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task UpsertExtractionResultAsync(ExtractionResult extraction, CancellationToken ct = default);
    Task UpdateExtractionResultAsync(ExtractionResult extraction, CancellationToken ct = default);
    Task UpdateExtractionSummaryAsync(Guid extractionId, string summaryJson, CancellationToken ct = default);
}
