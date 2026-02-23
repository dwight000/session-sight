using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface IExtractionResultRepository
{
    Task UpsertExtractionResultAsync(ExtractionResult extraction);
    Task UpdateExtractionResultAsync(ExtractionResult extraction);
    Task UpdateExtractionSummaryAsync(Guid extractionId, string summaryJson);
}
