using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SessionSight.Core.Entities;
using SessionSight.Core.Enums;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public partial class SessionRepository : ISessionRepository, IDocumentRepository, IExtractionResultRepository
{
    // Used by UpdateAsync(Session) only — document status updates use ExecuteUpdateAsync
    // which bypasses the change tracker and doesn't need concurrency retries.
    private const int MaxConcurrencyRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly SessionSightDbContext _context;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(SessionSightDbContext context, ILogger<SessionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IEnumerable<Session>> GetAllAsync(Guid? patientId = null, bool? hasDocument = null, CancellationToken ct = default)
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
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Session>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Where(s => s.PatientId == patientId)
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Session>> GetSessionsNeedingReindexAsync(
        Guid? patientId = null, Guid? sessionId = null, CancellationToken ct = default)
    {
        var query = _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Where(s => s.Document != null
                && (s.Document.Status == DocumentStatus.Completed
                    || s.Document.Status == DocumentStatus.PartiallyCompleted)
                && s.Document.IndexingStatus != IndexingStatus.Indexed);

        if (patientId.HasValue)
        {
            query = query.Where(s => s.PatientId == patientId.Value);
        }

        if (sessionId.HasValue)
        {
            query = query.Where(s => s.Id == sessionId.Value);
        }

        return await query
            .OrderBy(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Session> AddAsync(Session session, CancellationToken ct = default)
    {
        session.Id = Guid.NewGuid();
        session.CreatedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(ct);
        return session;
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
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
                await _context.Entry(session).ReloadAsync(ct);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrency conflict updating session {SessionId}, retry {Attempt}/{MaxRetries}")]
    private static partial void LogConcurrencyRetry(ILogger logger, Guid sessionId, int attempt, int maxRetries);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update session {SessionId} after {MaxRetries} concurrency retries")]
    private static partial void LogConcurrencyFailed(ILogger logger, Guid sessionId, int maxRetries);

    public async Task AddDocumentAsync(Session session, SessionDocument document, CancellationToken ct = default)
    {
        session.UpdatedAt = DateTime.UtcNow;
        session.Document = document;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> TryTransitionDocumentStatusAsync(Guid sessionId, DocumentStatus fromStatus, DocumentStatus toStatus, CancellationToken ct = default)
    {
        // When transitioning to Processing, reset resilience fields for a clean slate on retry
        if (toStatus == DocumentStatus.Processing && fromStatus != DocumentStatus.Processing)
        {
            var rows = await _context.Documents
                .Where(d => d.SessionId == sessionId && d.Status == fromStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, toStatus)
                    .SetProperty(d => d.FailureKind, FailureKind.None)
                    .SetProperty(d => d.ErrorMessage, (string?)null)
                    .SetProperty(d => d.IndexingStatus, IndexingStatus.None), ct);
            return rows > 0;
        }

        var affectedRows = await _context.Documents
            .Where(d => d.SessionId == sessionId && d.Status == fromStatus)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, toStatus), ct);
        return affectedRows > 0;
    }

    public async Task UpdateDocumentStatusAsync(Guid sessionId, DocumentStatus status, string? extractedText = null,
        IndexingStatus? indexingStatus = null, FailureKind? failureKind = null, string? errorMessage = null,
        CancellationToken ct = default)
    {
        // Use ExecuteUpdateAsync to bypass the EF change tracker entirely.
        // This avoids DbUpdateConcurrencyException caused by stale RowVersion
        // when TryTransitionDocumentStatusAsync (also ExecuteUpdateAsync) incremented
        // the DB RowVersion earlier in the same pipeline scope.
        var now = DateTime.UtcNow;
        var rows = await _context.Documents
            .Where(d => d.SessionId == sessionId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(d => d.Status, status)
                 .SetProperty(d => d.ProcessedAt,
                    d => status == DocumentStatus.Completed || status == DocumentStatus.PartiallyCompleted
                        ? now
                        : d.ProcessedAt)
                 .SetProperty(d => d.ExtractedText,
                    d => extractedText != null ? extractedText : d.ExtractedText)
                 .SetProperty(d => d.IndexingStatus,
                    d => indexingStatus.HasValue ? indexingStatus.Value : d.IndexingStatus)
                 .SetProperty(d => d.FailureKind,
                    d => failureKind.HasValue ? failureKind.Value : d.FailureKind)
                 .SetProperty(d => d.ErrorMessage,
                    d => errorMessage != null ? errorMessage : d.ErrorMessage),
            ct);

        if (rows == 0)
        {
            throw new InvalidOperationException($"No document found for session {sessionId}");
        }
    }

    public async Task<ExtractionResult?> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
        => await _context.Extractions
            .Include(e => e.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SessionId == sessionId, ct);

    public async Task UpsertExtractionResultAsync(ExtractionResult extraction, CancellationToken ct = default)
    {
        // Delete existing extraction for this session (if any) then insert new one.
        // Handles re-extraction after Failed status without unique constraint violation.
        await _context.Extractions
            .Where(e => e.SessionId == extraction.SessionId)
            .ExecuteDeleteAsync(ct);

        _context.Extractions.Add(extraction);
        await _context.SaveChangesAsync(ct);

        // Detach the placeholder so its empty Steps collection doesn't interfere
        // with EF change detection during subsequent step saves. The final update
        // uses ExecuteUpdateAsync which bypasses the change tracker entirely.
        _context.Entry(extraction).State = EntityState.Detached;
    }

    public async Task<IEnumerable<Session>> GetByPatientIdInDateRangeAsync(Guid patientId, DateOnly? startDate, DateOnly? endDate, CancellationToken ct = default)
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
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Session>> GetAllInDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Include(s => s.Patient)
            .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate)
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IEnumerable<Session>> GetFlaggedSessionsAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .Include(s => s.Patient)
            .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate)
            .Where(s => s.Extraction != null && s.Extraction.RequiresReview)
            .OrderByDescending(s => s.SessionDate)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task DeleteDocumentAsync(Guid sessionId, CancellationToken ct = default)
    {
        // Delete extraction first (depends on session, not document, but logically tied)
        await _context.Extractions
            .Where(e => e.SessionId == sessionId)
            .ExecuteDeleteAsync(ct);

        // Delete document
        await _context.Documents
            .Where(d => d.SessionId == sessionId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task UpdateExtractionResultAsync(ExtractionResult extraction, CancellationToken ct = default)
    {
        // Load the existing row (scalar only — no Include on Steps/Reviews)
        // and update via tracked change detection + SaveChangesAsync.
        // This avoids ExecuteUpdateAsync which bypasses the change tracker and
        // can be silently overwritten by a later SaveChangesAsync that flushes
        // stale tracked state from the same DbContext.
        var existing = await _context.Extractions
            .FirstOrDefaultAsync(e => e.Id == extraction.Id, ct)
            ?? throw new InvalidOperationException(
                $"UpdateExtractionResultAsync: extraction {extraction.Id} not found.");

        existing.SchemaVersion = extraction.SchemaVersion;
        existing.ModelUsed = extraction.ModelUsed;
        existing.OverallConfidence = extraction.OverallConfidence;
        existing.RequiresReview = extraction.RequiresReview;
        existing.ReviewStatus = extraction.ReviewStatus;
        existing.ReviewReasons = extraction.ReviewReasons;
        existing.ExtractedAt = extraction.ExtractedAt;
        existing.Data = extraction.Data;
        existing.SummaryJson = extraction.SummaryJson;
        existing.GuardrailApplied = extraction.GuardrailApplied;
        existing.HomicidalGuardrailApplied = extraction.HomicidalGuardrailApplied;
        existing.HomicidalGuardrailReason = extraction.HomicidalGuardrailReason;
        existing.SelfHarmGuardrailApplied = extraction.SelfHarmGuardrailApplied;
        existing.SelfHarmGuardrailReason = extraction.SelfHarmGuardrailReason;
        existing.CriteriaValidationAttempts = extraction.CriteriaValidationAttempts;
        existing.DiscrepancyCount = extraction.DiscrepancyCount;
        existing.ContentFilterBlocked = extraction.ContentFilterBlocked;
        existing.RiskFieldDecisionsJson = extraction.RiskFieldDecisionsJson;

        await _context.SaveChangesAsync(ct);

        // Detach to prevent stale state from interfering with subsequent operations
        _context.Entry(existing).State = EntityState.Detached;
    }

    public async Task UpdateExtractionSummaryAsync(Guid extractionId, string summaryJson, CancellationToken ct = default)
    {
        var extraction = await _context.Extractions.FindAsync([extractionId], ct);
        if (extraction is null)
        {
            throw new InvalidOperationException($"Extraction {extractionId} not found");
        }

        extraction.SummaryJson = summaryJson;
        await _context.SaveChangesAsync(ct);
    }
}
