using EmailAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailAnalyzer.Infrastructure.Persistence.Configurations;

public class EmailReplyConfiguration : IEntityTypeConfiguration<EmailReply>
{
    public void Configure(EntityTypeBuilder<EmailReply> builder)
    {
        builder.ToTable("EmailReplies");

        builder.HasKey(r => r.Id);

        // nvarchar(max)
        builder.Property(r => r.Body)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.IsAiGenerated)
            .IsRequired();

        // nvarchar(max), nullable
        builder.Property(r => r.AiRawResponse);

        builder.Property(r => r.CreatedDate)
            .IsRequired();

        builder.Property(r => r.SentMessageId)
            .HasMaxLength(300);

        builder.Property(r => r.LastSendError)
            .HasMaxLength(2000);

        builder.HasOne(r => r.EmailMessage)
            .WithOne(e => e.Reply)
            .HasForeignKey<EmailReply>(r => r.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // One reply per email for now; drop this to support reply history later.
        builder.HasIndex(r => r.EmailMessageId).IsUnique();

        builder.HasIndex(r => r.Status);
    }
}
