using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly SessionSightDbContext _context;

    public SessionRepository(SessionSightDbContext context)
    {
        _context = context;
    }

    public async Task<Session?> GetByIdAsync(Guid id)
        => await _context.Sessions
            .Include(s => s.Document)
            .Include(s => s.Extraction)
            .FirstOrDefaultAsync(s => s.Id == id);

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
        session.UpdatedAt = DateTime.UtcNow;
        // The entity should already be tracked from GetByIdAsync.
        // Just save changes - EF Core will handle inserting new related entities
        // and updating modified properties automatically.
        await _context.SaveChangesAsync();
    }

    public async Task AddDocumentAsync(Session session, SessionDocument document)
    {
        session.UpdatedAt = DateTime.UtcNow;
        session.Document = document;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
    }
}
