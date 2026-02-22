using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class ExtractionToolCallConfiguration : IEntityTypeConfiguration<ExtractionToolCall>
{
    public void Configure(EntityTypeBuilder<ExtractionToolCall> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Step)
            .WithMany(s => s.ToolCalls)
            .HasForeignKey(e => e.StepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.ToolName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.InputJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.OutputJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.StepId);
    }
}
