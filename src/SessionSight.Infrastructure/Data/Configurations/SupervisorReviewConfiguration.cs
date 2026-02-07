using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionSight.Core.Entities;

namespace SessionSight.Infrastructure.Data.Configurations;

public class SupervisorReviewConfiguration : IEntityTypeConfiguration<SupervisorReview>
{
    public void Configure(EntityTypeBuilder<SupervisorReview> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasOne(r => r.Extraction)
            .WithMany(e => e.Reviews)
            .HasForeignKey(r => r.ExtractionId);
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ReviewerName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(2000);
    }
}
