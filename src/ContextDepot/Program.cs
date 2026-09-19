using System.Text.Json;
using System.Text.Json.Serialization;
using ContextDepot.Application;
using ContextDepot.Background;
using ContextDepot.Infrastructure;
using ContextDepot.HealthChecks;
using ContextDepot.MCP.Contexts;
using ContextDepot.MCP.Documents;
using ContextDepot.MCP.Owners;
using ContextDepot.MCP.Shared;
using ContextDepot.MCP.Workspaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using Serilog;

// The first phase of two-stage initialization uses a console-only bootstrap logger
// to capture fatal errors before the host is built (configuration and DI setup).
// UseSerilog replaces it with the complete appsettings-based configuration.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ContextDepot host");

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
    builder.Services.AddContextDepotInfrastructure(builder.Configuration);
    builder.Services.AddHostedService<IndexRepairHostedService>();
    builder.Services
        .AddMcpServer(options => options.ServerInstructions = ContextDepotMCPInstructions.Text)
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithTools<ContextTools>()
        .WithTools<DocumentTools>()
        .WithTools<WorkspaceTools>()
        .WithTools<OwnerTools>();

    var app = builder.Build();

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
