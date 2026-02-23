using SessionSight.Core.Entities;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Interfaces;

public interface IDocumentRepository
{
    Task AddDocumentAsync(Session session, SessionDocument document);
    Task UpdateDocumentStatusAsync(Guid sessionId, DocumentStatus status, string? extractedText = null);
    Task<bool> TryTransitionDocumentStatusAsync(Guid sessionId, DocumentStatus fromStatus, DocumentStatus toStatus);
    Task DeleteDocumentAsync(Guid sessionId);
}
