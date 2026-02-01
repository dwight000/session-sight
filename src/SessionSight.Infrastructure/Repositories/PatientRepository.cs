using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly SessionSightDbContext _context;

    public PatientRepository(SessionSightDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Patient>> GetAllAsync()
        => await _context.Patients.AsNoTracking().ToListAsync();

    public async Task<Patient?> GetByIdAsync(Guid id)
        => await _context.Patients.FindAsync(id);

    public async Task<Patient?> GetByExternalIdAsync(string externalId)
        => await _context.Patients.FirstOrDefaultAsync(p => p.ExternalId == externalId);

    public async Task<Patient> AddAsync(Patient patient)
    {
        patient.Id = Guid.NewGuid();
        patient.CreatedAt = DateTime.UtcNow;
        patient.UpdatedAt = DateTime.UtcNow;
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();
        return patient;
    }

    public async Task UpdateAsync(Patient patient)
    {
        patient.UpdatedAt = DateTime.UtcNow;
        _context.Patients.Update(patient);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var patient = await _context.Patients.FindAsync(id);
        if (patient != null)
        {
            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();
        }
    }
}
