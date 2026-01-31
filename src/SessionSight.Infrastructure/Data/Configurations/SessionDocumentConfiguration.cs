using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class SessionDocumentConfiguration : IEntityTypeConfiguration<SessionDocument>
{
    public void Configure(EntityTypeBuilder<SessionDocument> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasOne(d => d.Session).WithOne(s => s.Document).HasForeignKey<SessionDocument>(d => d.SessionId);
        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.BlobUri).HasMaxLength(2048).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50);
    }
}
