using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public partial class SessionRepository : ISessionRepository
{
    private const int MaxConcurrencyRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly SessionSightDbContext _context;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(SessionSightDbContext context, ILogger<SessionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Session?> GetByIdAsync(Guid id)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IEnumerable<Session>> GetAllAsync(Guid? patientId = null, bool? hasDocument = null)
    {
        var query = _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Patient)
            .AsQueryable();

        if (patientId.HasValue)
        {
            query = query.Where(s => s.PatientId == patientId.Value);
        }

        if (hasDocument.HasValue)
        {
            query = hasDocument.Value
                ? query.Where(s => s.Document != null)
                : query.Where(s => s.Document == null);
        }

        return await query
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Session>> GetByPatientIdAsync(Guid patientId)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Where(s => s.PatientId == patientId)
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync();

    public async Task<Session> AddAsync(Session session)
    {
        session.Id = Guid.NewGuid();
        session.CreatedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task UpdateAsync(Session session)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt == MaxConcurrencyRetries)
                {
                    LogConcurrencyFailed(_logger, session.Id, MaxConcurrencyRetries);
                    throw new InvalidOperationException(
                        $"Failed to update session {session.Id} after {MaxConcurrencyRetries} attempts due to concurrency conflicts", ex);
                }

                LogConcurrencyRetry(_logger, session.Id, attempt, MaxConcurrencyRetries);
                await _context.Entry(session).ReloadAsync();
                await Task.Delay(RetryDelay);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrency conflict updating session {SessionId}, retry {Attempt}/{MaxRetries}")]
    private static partial void LogConcurrencyRetry(ILogger logger, Guid sessionId, int attempt, int maxRetries);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update session {SessionId} after {MaxRetries} concurrency retries")]
    private static partial void LogConcurrencyFailed(ILogger logger, Guid sessionId, int maxRetries);

    public async Task AddDocumentAsync(Session session, SessionDocument document)
    {
        session.UpdatedAt = DateTime.UtcNow;
        session.Document = document;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateDocumentStatusAsync(Guid sessionId, DocumentStatus status, string? extractedText = null)
    {
        // Direct update to Document table only - avoids Session RowVersion concurrency issues
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.SessionId == sessionId);

        if (document is null)
        {
            throw new InvalidOperationException($"No document found for session {sessionId}");
        }

        document.Status = status;
        if (status == DocumentStatus.Completed)
        {
            document.ProcessedAt = DateTime.UtcNow;
        }
        if (extractedText != null)
        {
            document.ExtractedText = extractedText;
        }

        await _context.SaveChangesAsync();
    }

    public async Task SaveExtractionResultAsync(ExtractionResult extraction)
    {
        // Direct insert to Extractions table - avoids Session RowVersion concurrency issues
        _context.Extractions.Add(extraction);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Session>> GetByPatientIdInDateRangeAsync(Guid patientId, DateOnly? startDate, DateOnly? endDate)
    {
        var query = _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Include(s => s.Patient)
            .Where(s => s.PatientId == patientId);

        if (startDate.HasValue)
        {
            query = query.Where(s => s.SessionDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.SessionDate <= endDate.Value);
        }

        return await query
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Session>> GetAllInDateRangeAsync(DateOnly startDate, DateOnly endDate)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Include(s => s.Patient)
            .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate)
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync();

    public async Task<IEnumerable<Session>> GetFlaggedSessionsAsync(DateOnly startDate, DateOnly endDate)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Include(s => s.Patient)
            .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate)
            .Where(s => s.Extraction != null && s.Extraction.RequiresReview)
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync();

    public async Task UpdateExtractionSummaryAsync(Guid extractionId, string summaryJson)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId);
        if (extraction is null)
        {
            throw new InvalidOperationException($"Extraction {extractionId} not found");
        }

        extraction.SummaryJson = summaryJson;
        await _context.SaveChangesAsync();
    }
}
