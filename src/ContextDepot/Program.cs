using System.Text.Json;
using System.Text.Json.Serialization;
using ContextDepot.Application;
using ContextDepot.Application.DataProtection;
using ContextDepot.BackgroundServices;
using ContextDepot.Infrastructure;
using ContextDepot.Infrastructure.Embeddings;
using ContextDepot.HealthChecks;
using ContextDepot.Infrastructure.DataProtection;
using ContextDepot.MCP.Contexts;
using ContextDepot.MCP.Documents;
using ContextDepot.MCP.Depots;
using ContextDepot.MCP.Shared;
using ContextDepot.MCP.Workspaces;
using ContextDepot.Middlewares;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using ModelContextProtocol.AspNetCore;
using Serilog;

// Load the same configuration before the host is built so bootstrap failures
// use the configured console and file sinks. UseSerilog replaces it with the
// complete host-backed configuration once the host is available.
var bootstrapConfiguration = BuildBootstrapConfiguration(args);
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(bootstrapConfiguration)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ContextDepot");

    var builder = WebApplication.CreateBuilder(args);

    var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
    if (string.IsNullOrWhiteSpace(keyRingPath))
    {
        if (builder.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                "DataProtection:KeyRingPath must point to persistent shared storage in production.");
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyRingPath must be configured when no local application-data directory is available.");
        }

        keyRingPath = Path.Combine(
            localApplicationData,
            "ContextDepot",
            "DataProtectionKeys");
    }

    if (builder.Environment.IsProduction() && !Path.IsPathRooted(keyRingPath))
    {
        throw new InvalidOperationException(
            "DataProtection:KeyRingPath must be an absolute path to persistent shared storage in production.");
    }

    var fullKeyRingPath = Path.GetFullPath(keyRingPath, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(fullKeyRingPath);
    builder.Services.AddDataProtection()
        .SetApplicationName("ContextDepot")
        .PersistKeysToFileSystem(new DirectoryInfo(fullKeyRingPath));
    builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

    builder.Host.UseSerilog((context, logger) => logger
        .ReadFrom.Configuration(context.Configuration));
    builder.Services.AddHealthChecks();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });
    builder.Services.AddContextDepotApplication(builder.Configuration);
    builder.Services.AddContextDepotEmbeddingProvider();
    builder.Services.AddContextDepotInfrastructure(builder.Configuration);
    builder.Services.AddHostedService<IndexRepairHostedService>();
    builder.Services.AddHostedService<DatabaseApplicationSettingsReloadService>();

    builder.Services
        .AddMcpServer(options => options.ServerInstructions = ContextDepotMCPInstructions.Text)
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithTools<ContextTools>()
        .WithTools<DocumentTools>()
        .WithTools<WorkspaceTools>()
        .WithTools<DepotTools>();

    var app = builder.Build();

    await app.Services
        .GetRequiredService<ContextDepotStartupInitializer>()
        .InitializeAsync(CancellationToken.None);

    app.UseMiddleware<DepotAccessKeyAuthenticationMiddleware>();
    app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
    app.MapHealthChecks("/readyz", new HealthCheckOptions
    {
        ResponseWriter = ReadinessResponseWriter.WriteAsync
    });
    app.MapMcp("/mcp");

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    // HostAbortedException is raised by design-time tools such as `dotnet ef`;
    // it is expected control flow and should not be logged as fatal.
    Log.Fatal(exception, "ContextDepot host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

static IConfiguration BuildBootstrapConfiguration(string[] args)
{
    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (string.IsNullOrWhiteSpace(environmentName))
    {
        environmentName = "Production";
    }

    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();

    return configuration;
}