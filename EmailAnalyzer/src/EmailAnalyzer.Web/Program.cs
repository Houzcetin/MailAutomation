using System.Text.Json.Serialization;
using EmailAnalyzer.Infrastructure;
using EmailAnalyzer.Web.Middleware;
using EmailAnalyzer.Web.Workers;
using Serilog;

// Bootstrap logger — captures failures during startup, before the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EmailAnalyzer.Web");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog: console + daily rolling file under logs/.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));

    // Infrastructure (EF Core / SQL Server + OpenAI analysis service + MailKit fetch service).
    builder.Services.AddInfrastructure(builder.Configuration);

    // Background worker: polls the mailbox and analyses new mail.
    builder.Services.AddHostedService<EmailPollingService>();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Serialize enums as their names (e.g. "MuhasebeFinans") in API responses.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // Razor Pages for the Turkish admin panel (spec section 9).
    builder.Services.AddRazorPages();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Global exception handling — must be first so it wraps everything below.
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSerilogRequestLogging();

    // Swagger enabled always (demo stage — no auth per spec).
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseStaticFiles();

    app.MapControllers();
    app.MapRazorPages();

    // Land on the admin dashboard by default.
    app.MapGet("/", () => Results.Redirect("/admin"));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "EmailAnalyzer.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
