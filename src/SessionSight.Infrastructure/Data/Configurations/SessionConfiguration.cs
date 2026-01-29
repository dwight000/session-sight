using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.PatientId, s.SessionDate });
        builder.HasOne(s => s.Patient).WithMany(p => p.Sessions).HasForeignKey(s => s.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Therapist).WithMany(t => t.Sessions).HasForeignKey(s => s.TherapistId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(s => s.SessionType).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Modality).HasConversion<string>().HasMaxLength(50);
    }
}
