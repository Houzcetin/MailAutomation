using EmailAnalyzer.Domain.Entities;
using EmailAnalyzer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailAnalyzer.Infrastructure.Persistence.Configurations;

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("EmailMessages");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MessageId)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(e => e.SenderEmail)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(e => e.SenderName)
            .HasMaxLength(200);

        builder.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(500);

        // nvarchar(max)
        builder.Property(e => e.Body)
            .IsRequired();

        builder.Property(e => e.ReceivedDate)
            .IsRequired();

        // Enums stored as strings for readability / stable persistence.
        builder.Property(e => e.MainCategory)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.SubCategory)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(e => e.Priority)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Sentiment)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Summary)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.AiConfidenceScore)
            .HasColumnType("decimal(4,3)");

        builder.Property(e => e.IsProcessed)
            .IsRequired();

        builder.Property(e => e.RequiresHumanReview)
            .IsRequired();

        // nvarchar(max), nullable
        builder.Property(e => e.AiRawResponse);

        builder.Property(e => e.CreatedDate)
            .IsRequired();

        // Unique index — the same mail is never processed twice.
        builder.HasIndex(e => e.MessageId).IsUnique();

        // Query indexes (spec section 4).
        builder.HasIndex(e => e.ReceivedDate);
        builder.HasIndex(e => e.MainCategory);
        builder.HasIndex(e => e.Priority);
        builder.HasIndex(e => e.RequiresHumanReview);
    }
}
