using SessionSight.Core.Entities;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Interfaces;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id);
    Task<IEnumerable<Session>> GetByPatientIdAsync(Guid patientId);
    Task<Session> AddAsync(Session session);
    Task UpdateAsync(Session session);
    Task AddDocumentAsync(Session session, SessionDocument document);
    Task UpdateDocumentStatusAsync(Guid sessionId, DocumentStatus status, string? extractedText = null);
    Task SaveExtractionResultAsync(ExtractionResult extraction);
}
