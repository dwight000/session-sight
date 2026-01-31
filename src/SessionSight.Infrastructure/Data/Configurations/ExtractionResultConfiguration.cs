using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;
using SessionSight.Core.Schema;

namespace SessionSight.Infrastructure.Data.Configurations;

public class ExtractionResultConfiguration : IEntityTypeConfiguration<ExtractionResult>
{
    public void Configure(EntityTypeBuilder<ExtractionResult> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.Session).WithOne(s => s.Extraction).HasForeignKey<ExtractionResult>(e => e.SessionId);
        builder.Property(e => e.SchemaVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ModelUsed).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Data).HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<ClinicalExtraction>(v, (JsonSerializerOptions?)null)!
        ).HasColumnType("nvarchar(max)");
    }
}
