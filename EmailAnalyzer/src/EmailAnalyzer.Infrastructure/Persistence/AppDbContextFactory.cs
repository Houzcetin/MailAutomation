using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmailAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef` (migrations add / database update) so the
/// tooling can build an AppDbContext without booting the Web host. The connection
/// string comes from the EMAILANALYZER_CONN environment variable, falling back to a
/// local SQL Server Express default. Runtime connection strings come from configuration.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnectionString =
        @"Server=.\SQLEXPRESS;Database=EmailAnalyzerDb;Trusted_Connection=True;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("EMAILANALYZER_CONN") ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
