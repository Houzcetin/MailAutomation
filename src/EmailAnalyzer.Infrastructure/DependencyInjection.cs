using System.Net.Http.Headers;
using System.Text;
using EmailAnalyzer.Domain.Services;
using EmailAnalyzer.Infrastructure.Ai;
using EmailAnalyzer.Infrastructure.Configuration;
using EmailAnalyzer.Infrastructure.Mail;
using EmailAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EmailAnalyzer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure services. Later phases add the MailKit fetch service and
    /// the polling background service here.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Enable legacy code pages (e.g. ISO-8859-9 / windows-1254) so MimeKit can decode
        // Turkish emails whose bodies declare or default to a non-UTF-8 charset. Without this,
        // characters like ı/ş/ğ come out garbled ("mizan" instead of "mizan").
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Options binding.
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<AnalysisOptions>(configuration.GetSection(AnalysisOptions.SectionName));
        services.Configure<GmailOptions>(configuration.GetSection(GmailOptions.SectionName));
        services.Configure<ReplyOptions>(configuration.GetSection(ReplyOptions.SectionName));

        // OpenAI typed HttpClient — base URL + Bearer auth from configuration.
        services.AddHttpClient<OpenAiClient>((provider, client) =>
        {
            var openAi = provider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(openAi.BaseUrl));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        });

        services.AddScoped<IEmailAnalysisService, EmailAnalysisService>();
        services.AddScoped<IEmailFetchService, MailKitEmailFetchService>();
        services.AddScoped<IReplyGenerationService, ReplyGenerationService>();
        services.AddScoped<IEmailReplySender, MailKitEmailReplySender>();

        return services;
    }

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
