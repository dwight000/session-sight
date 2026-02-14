using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
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
}
