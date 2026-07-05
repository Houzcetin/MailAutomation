using EmailAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailAnalyzer.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    public DbSet<EmailReply> EmailReplies => Set<EmailReply>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
