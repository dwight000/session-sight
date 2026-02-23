using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Session>> GetAllAsync(Guid? patientId = null, bool? hasDocument = null, CancellationToken ct = default);
    Task<IEnumerable<Session>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<IEnumerable<Session>> GetByPatientIdInDateRangeAsync(Guid patientId, DateOnly? startDate, DateOnly? endDate, CancellationToken ct = default);
    Task<IEnumerable<Session>> GetAllInDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);
    Task<IEnumerable<Session>> GetFlaggedSessionsAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);
    Task<Session> AddAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
}
