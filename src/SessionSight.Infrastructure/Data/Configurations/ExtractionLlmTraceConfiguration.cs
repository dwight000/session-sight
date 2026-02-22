using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class ExtractionLlmTraceConfiguration : IEntityTypeConfiguration<ExtractionLlmTrace>
{
    public void Configure(EntityTypeBuilder<ExtractionLlmTrace> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Step)
            .WithMany(s => s.LlmTraces)
            .HasForeignKey(e => e.StepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.ModelUsed).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PromptText).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ResponseText).HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.StepId);
    }
}
