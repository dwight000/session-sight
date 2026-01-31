using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.JobKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(j => j.JobKey).IsUnique();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(50);
    }
}
