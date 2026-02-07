using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        builder.Property(e => e.ReviewStatus).HasConversion<string>().HasMaxLength(20);

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.ReviewReasons).HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
        ).HasColumnType("nvarchar(max)")
         .Metadata.SetValueComparer(stringListComparer);
    }
}
