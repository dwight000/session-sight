using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id);
    Task<IEnumerable<Session>> GetAllAsync(Guid? patientId = null, bool? hasDocument = null);
    Task<IEnumerable<Session>> GetByPatientIdAsync(Guid patientId);
    Task<IEnumerable<Session>> GetByPatientIdInDateRangeAsync(Guid patientId, DateOnly? startDate, DateOnly? endDate);
    Task<IEnumerable<Session>> GetAllInDateRangeAsync(DateOnly startDate, DateOnly endDate);
    Task<IEnumerable<Session>> GetFlaggedSessionsAsync(DateOnly startDate, DateOnly endDate);
    Task<Session> AddAsync(Session session);
    Task UpdateAsync(Session session);
}
