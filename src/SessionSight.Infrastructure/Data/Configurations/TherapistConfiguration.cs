using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class TherapistConfiguration : IEntityTypeConfiguration<Therapist>
{
    public void Configure(EntityTypeBuilder<Therapist> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.LicenseNumber).HasMaxLength(50);
        builder.Property(t => t.Credentials).HasMaxLength(50);

        builder.HasData(new Therapist
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Default Therapist",
            IsActive = true,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
