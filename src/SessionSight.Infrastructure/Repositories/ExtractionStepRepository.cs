using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public class ExtractionStepRepository : IExtractionStepRepository
{
    private readonly SessionSightDbContext _context;

    public ExtractionStepRepository(SessionSightDbContext context)
    {
        _context = context;
    }

    public async Task SaveStepAsync(ExtractionStep extractionStep, CancellationToken ct = default)
    {
        _context.ExtractionSteps.Add(extractionStep);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExtractionStep>> GetStepsByExtractionIdAsync(Guid extractionId, CancellationToken ct = default)
    {
        return await _context.ExtractionSteps
            .Include(s => s.ToolCalls)
            .Include(s => s.LlmTraces)
            .Where(s => s.ExtractionId == extractionId)
            .OrderBy(s => s.StepOrder)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
