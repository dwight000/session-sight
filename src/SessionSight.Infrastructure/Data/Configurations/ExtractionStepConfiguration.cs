using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class ExtractionStepConfiguration : IEntityTypeConfiguration<ExtractionStep>
{
    public void Configure(EntityTypeBuilder<ExtractionStep> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Extraction)
            .WithMany(er => er.Steps)
            .HasForeignKey(e => e.ExtractionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.StepName).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ModelUsed).HasMaxLength(100);
        builder.Property(e => e.ResultSummaryJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
        builder.Property(e => e.EstimatedCostUsd).HasPrecision(10, 6);

        builder.HasIndex(e => e.ExtractionId);
    }
}
