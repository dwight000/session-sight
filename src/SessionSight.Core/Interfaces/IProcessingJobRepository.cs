using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface IProcessingJobRepository
{
    Task<IEnumerable<ProcessingJob>> GetAllAsync();
    Task<ProcessingJob?> GetByIdAsync(Guid id);
}
