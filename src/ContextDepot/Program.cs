using System.Text.Json;
using System.Text.Json.Serialization;
using ContextDepot.Application;
using ContextDepot.BackgroundServices;
using ContextDepot.Infrastructure;
using ContextDepot.Infrastructure.Embeddings;
using ContextDepot.HealthChecks;
using ContextDepot.MCP.Contexts;
using ContextDepot.MCP.Documents;
using ContextDepot.MCP.Depots;
using ContextDepot.MCP.Authentication;
using ContextDepot.MCP.Shared;
using ContextDepot.MCP.Workspaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.AspNetCore;
using Serilog;

// The first phase of two-stage initialization also writes to the application log
// so fatal errors during configuration and DI setup are not lost before the host
// is built. UseSerilog replaces it with the complete appsettings-based configuration.
var bootstrapLogPath = ResolveBootstrapLogPath();
var bootstrapLogDirectory = Path.GetDirectoryName(Path.GetFullPath(bootstrapLogPath));
if (!string.IsNullOrWhiteSpace(bootstrapLogDirectory))
{
    Directory.CreateDirectory(bootstrapLogDirectory);
}

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: bootstrapLogPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        fileSizeLimitBytes: 10_485_760,
        rollOnFileSizeLimit: true,
        shared: true,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ContextDepot");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, logger) => logger
        .ReadFrom.Configuration(context.Configuration));
    builder.Services.AddHealthChecks();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });
    builder.Services.AddContextDepotApplication(builder.Configuration);
    builder.Services.AddContextDepotEmbeddingProvider(builder.Configuration);
    builder.Services.AddContextDepotInfrastructure(builder.Configuration);
    builder.Services.AddHostedService<IndexRepairHostedService>();
    builder.Services
        .AddMcpServer(options => options.ServerInstructions = ContextDepotMCPInstructions.Text)
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithTools<ContextTools>()
        .WithTools<DocumentTools>()
        .WithTools<WorkspaceTools>()
        .WithTools<DepotTools>();

    var app = builder.Build();

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

static string ResolveBootstrapLogPath()
{
    var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

    if (!string.IsNullOrWhiteSpace(environmentName))
    {
        configuration.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
    }

    var path = configuration
        .AddEnvironmentVariables()
        .Build()["Serilog:WriteTo:1:Args:path"];

    return string.IsNullOrWhiteSpace(path) ? "logs/context-depot-.log" : path;
}
