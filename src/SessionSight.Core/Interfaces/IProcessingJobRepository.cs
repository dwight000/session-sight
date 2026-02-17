using SessionSight.Core.Entities;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Interfaces;

public interface IProcessingJobRepository
{
    Task<IEnumerable<ProcessingJob>> GetAllAsync();
    Task<ProcessingJob?> GetByIdAsync(Guid id);
    Task<ProcessingJob?> GetByJobKeyAsync(string jobKey);
    Task<ProcessingJob> CreateAsync(ProcessingJob job);
    Task UpdateStatusAsync(string jobKey, JobStatus status);
}
