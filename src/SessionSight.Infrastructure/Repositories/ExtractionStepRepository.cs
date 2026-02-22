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
        var exists = await _context.ExtractionSteps
            .AsNoTracking()
            .AnyAsync(s => s.Id == extractionStep.Id, ct);

        if (!exists)
        {
            // Detach in case a prior failed save left it tracked
            var entry = _context.Entry(extractionStep);
            if (entry.State != EntityState.Detached)
                entry.State = EntityState.Detached;

            _context.ExtractionSteps.Add(extractionStep);
            await _context.SaveChangesAsync(ct);
            // Detach after insert so subsequent updates don't conflict
            _context.Entry(extractionStep).State = EntityState.Detached;
        }
        else
        {
            // Bypass change tracker to avoid concurrency conflicts with other
            // tracked entities (Session, ExtractionResult) in the same DbContext.
            await _context.ExtractionSteps
                .Where(s => s.Id == extractionStep.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Status, extractionStep.Status)
                    .SetProperty(s => s.CompletedAt, extractionStep.CompletedAt)
                    .SetProperty(s => s.DurationMs, extractionStep.DurationMs)
                    .SetProperty(s => s.ModelUsed, extractionStep.ModelUsed)
                    .SetProperty(s => s.InputTokens, extractionStep.InputTokens)
                    .SetProperty(s => s.OutputTokens, extractionStep.OutputTokens)
                    .SetProperty(s => s.TotalTokens, extractionStep.TotalTokens)
                    .SetProperty(s => s.ResultSummaryJson, extractionStep.ResultSummaryJson)
                    .SetProperty(s => s.ErrorMessage, extractionStep.ErrorMessage),
                ct);

            // Insert child entities directly (they only exist after step completes)
            if (extractionStep.ToolCalls.Count > 0)
            {
                // Ensure tool calls are not tracked from a prior attempt
                foreach (var tc in extractionStep.ToolCalls)
                    _context.Entry(tc).State = EntityState.Detached;

                _context.Set<ExtractionToolCall>().AddRange(extractionStep.ToolCalls);
                await _context.SaveChangesAsync(ct);

                foreach (var tc in extractionStep.ToolCalls)
                    _context.Entry(tc).State = EntityState.Detached;
            }

            if (extractionStep.LlmTraces.Count > 0)
            {
                foreach (var lt in extractionStep.LlmTraces)
                    _context.Entry(lt).State = EntityState.Detached;

                _context.Set<ExtractionLlmTrace>().AddRange(extractionStep.LlmTraces);
                await _context.SaveChangesAsync(ct);

                foreach (var lt in extractionStep.LlmTraces)
                    _context.Entry(lt).State = EntityState.Detached;
            }
        }
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
