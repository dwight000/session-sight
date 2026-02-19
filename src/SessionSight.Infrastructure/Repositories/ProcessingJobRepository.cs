using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public class ProcessingJobRepository : IProcessingJobRepository
{
    private readonly SessionSightDbContext _context;

    public ProcessingJobRepository(SessionSightDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProcessingJob>> GetAllAsync()
        => await _context.ProcessingJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

    public async Task<ProcessingJob?> GetByIdAsync(Guid id)
        => await _context.ProcessingJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);

    public async Task<ProcessingJob?> GetByJobKeyAsync(string jobKey)
        => await _context.ProcessingJobs.AsNoTracking().FirstOrDefaultAsync(j => j.JobKey == jobKey);

    public async Task<ProcessingJob> CreateAsync(ProcessingJob job)
    {
        job.Id = Guid.NewGuid();
        job.CreatedAt = DateTime.UtcNow;
        _context.ProcessingJobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task UpdateStatusAsync(string jobKey, JobStatus status)
    {
        await _context.ProcessingJobs
            .Where(j => j.JobKey == jobKey)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, status)
                .SetProperty(j => j.CompletedAt, DateTime.UtcNow));
    }
}
