using SessionSight.Core.Entities;
using SessionSight.Core.Enums;

namespace SessionSight.Core.Interfaces;

public interface IDocumentRepository
{
    Task AddDocumentAsync(Session session, SessionDocument document, CancellationToken ct = default);
    Task UpdateDocumentStatusAsync(Guid sessionId, DocumentStatus status, string? extractedText = null, CancellationToken ct = default);
    Task<bool> TryTransitionDocumentStatusAsync(Guid sessionId, DocumentStatus fromStatus, DocumentStatus toStatus, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid sessionId, CancellationToken ct = default);
}
