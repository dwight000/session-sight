using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SessionSight.Core.Entities;
using SessionSight.Core.Interfaces;
using SessionSight.Infrastructure.Data;

namespace SessionSight.Infrastructure.Repositories;

public partial class PatientRepository : IPatientRepository
{
    private const int MaxUpsertRetries = 3;
    private readonly SessionSightDbContext _context;
    private readonly ILogger<PatientRepository> _logger;

    public PatientRepository(SessionSightDbContext context, ILogger<PatientRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Patient>> GetAllAsync()
        => await _context.Patients.AsNoTracking().ToListAsync();

    public async Task<Patient?> GetByIdAsync(Guid id)
        => await _context.Patients.FindAsync(id);

    public async Task<Patient?> GetByExternalIdAsync(string externalId)
        => await _context.Patients.FirstOrDefaultAsync(p => p.ExternalId == externalId);

    public async Task<Patient> GetOrCreateByExternalIdAsync(string externalId, string firstName, string lastName)
    {
        for (var attempt = 1; attempt <= MaxUpsertRetries; attempt++)
        {
            var existing = await _context.Patients.FirstOrDefaultAsync(p => p.ExternalId == externalId);
            if (existing is not null)
                return existing;

            try
            {
                var patient = new Patient
                {
                    Id = Guid.NewGuid(),
                    ExternalId = externalId,
                    FirstName = firstName,
                    LastName = lastName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
                return patient;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                LogUniqueConstraintRetry(_logger, externalId, attempt, MaxUpsertRetries);
                // Detach the failed entity so the next iteration can re-query
                foreach (var entry in _context.ChangeTracker.Entries<Patient>()
                    .Where(e => e.Entity.ExternalId == externalId && e.State == EntityState.Added))
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        // Final read after all retries exhausted
        return await _context.Patients.FirstAsync(p => p.ExternalId == externalId);
    }

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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_Patients_ExternalId", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unique constraint violation for patient ExternalId={ExternalId}, retry {Attempt}/{MaxRetries}")]
    private static partial void LogUniqueConstraintRetry(ILogger logger, string externalId, int attempt, int maxRetries);
}
