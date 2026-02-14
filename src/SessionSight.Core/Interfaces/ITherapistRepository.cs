using SessionSight.Core.Entities;

namespace SessionSight.Core.Interfaces;

public interface ITherapistRepository
{
    Task<IEnumerable<Therapist>> GetAllAsync();
    Task<Therapist?> GetByIdAsync(Guid id);
    Task<Therapist> AddAsync(Therapist therapist);
    Task UpdateAsync(Therapist therapist);
    Task DeleteAsync(Guid id);
}
