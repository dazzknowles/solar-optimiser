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
