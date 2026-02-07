using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SessionSight.Core.Entities;
using SessionSight.Core.Schema;

namespace SessionSight.Infrastructure.Data;

public class SessionSightDbContext : DbContext
{
    public SessionSightDbContext(DbContextOptions<SessionSightDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Therapist> Therapists => Set<Therapist>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionDocument> Documents => Set<SessionDocument>();
    public DbSet<ExtractionResult> Extractions => Set<ExtractionResult>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<SupervisorReview> SupervisorReviews => Set<SupervisorReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SessionSightDbContext).Assembly);
    }
}
