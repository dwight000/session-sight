using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public class TherapistRepository : ITherapistRepository
{
    private readonly SessionSightDbContext _context;

    public TherapistRepository(SessionSightDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Therapist>> GetAllAsync()
        => await _context.Therapists.AsNoTracking().ToListAsync();

    public async Task<Therapist?> GetByIdAsync(Guid id)
        => await _context.Therapists.FindAsync(id);

    public async Task<Therapist> AddAsync(Therapist therapist)
    {
        therapist.Id = Guid.NewGuid();
        therapist.CreatedAt = DateTime.UtcNow;
        therapist.UpdatedAt = DateTime.UtcNow;
        _context.Therapists.Add(therapist);
        await _context.SaveChangesAsync();
        return therapist;
    }

    public async Task UpdateAsync(Therapist therapist)
    {
        therapist.UpdatedAt = DateTime.UtcNow;
        _context.Therapists.Update(therapist);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var therapist = await _context.Therapists.FindAsync(id);
        if (therapist != null)
        {
            _context.Therapists.Remove(therapist);
            await _context.SaveChangesAsync();
        }
    }
}
