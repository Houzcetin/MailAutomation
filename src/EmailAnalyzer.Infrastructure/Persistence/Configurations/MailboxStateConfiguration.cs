using EmailAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailAnalyzer.Infrastructure.Persistence.Configurations;

public class MailboxStateConfiguration : IEntityTypeConfiguration<MailboxState>
{
    public void Configure(EntityTypeBuilder<MailboxState> builder)
    {
        builder.ToTable("MailboxStates");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Folder)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.UidValidity)
            .IsRequired();

        builder.Property(s => s.LastProcessedUid)
            .IsRequired();

        // One watermark row per folder.
        builder.HasIndex(s => s.Folder).IsUnique();
    }
}
