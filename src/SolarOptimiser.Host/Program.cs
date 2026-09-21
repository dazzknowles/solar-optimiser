using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolarOptimiser.Collection;
using SolarOptimiser.Host;
using SolarOptimiser.Host.Endpoints;
using SolarOptimiser.Host.Security;
using SolarOptimiser.Persistence;
using SolarOptimiser.Providers.FoxESS;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IReadOnlyDictionary<string, string> envFile = EnvFile.Load();
Dictionary<string, string?> envOverrides = new(StringComparer.Ordinal);

if (envFile.TryGetValue("DB_HOST", out string? dbHost)
    && envFile.TryGetValue("DB_PORT", out string? dbPort)
    && envFile.TryGetValue("DB_NAME", out string? dbName)
    && envFile.TryGetValue("DB_USER", out string? dbUser)
    && envFile.TryGetValue("DB_PASSWORD", out string? dbPassword))
{
    envOverrides["ConnectionStrings:SolarOptimiser"] = $"Server={dbHost};Port={dbPort};Database={dbName};User Id={dbUser};Password={dbPassword};";
}

if (envFile.TryGetValue("FOXESS_API_KEY", out string? foxEssApiKey) && !string.IsNullOrWhiteSpace(foxEssApiKey))
{
    envOverrides["FoxESSProviderOptions:ApiKey"] = foxEssApiKey;
}

if (envFile.TryGetValue("FOXESS_SITE_PROVIDER_IDS", out string? foxEssSiteProviderIds) && !string.IsNullOrWhiteSpace(foxEssSiteProviderIds))
{
    string[] siteProviderIds = foxEssSiteProviderIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (int index = 0; index < siteProviderIds.Length; index++)
    {
        envOverrides[$"FoxESSProviderOptions:SiteProviderIDs:{index}"] = siteProviderIds[index];
        envOverrides[$"CollectionOptions:ProviderSiteIDs:{index}"] = siteProviderIds[index];
    }
}

if (envOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(envOverrides);
}

string? sentryDsn = builder.Configuration["SentryOptions:SentryDsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    Sentry.SentrySdk.Init(options =>
    {
        options.Dsn = sentryDsn;
    });
}

builder.Services.Configure<SolarHostOptions>(builder.Configuration.GetSection("HostOptions"));
builder.Services.AddSolarOptimiserProvidersFoxESS(builder.Configuration);
builder.Services.AddSolarOptimiserPersistence(builder.Configuration);
builder.Services.AddSolarOptimiserCollection(builder.Configuration);
builder.Services.AddHostedService<CollectionPollingService>();

WebApplication app = builder.Build();

// SOL-T-1101: fails to start on missing/invalid CIDR configuration - CidrAllowlistMiddleware's constructor
// (which runs when this middleware is first built into the pipeline, i.e. at startup) validates and throws.
app.UseMiddleware<CidrAllowlistMiddleware>();

app.MapTelemetryEndpoints();

app.Run();
