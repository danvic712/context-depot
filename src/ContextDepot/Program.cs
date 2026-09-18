using Serilog;
using ContextDepot.Infrastructure.Persistence;
using ContextDepot.Infrastructure.CurrentOwner;
using ContextDepot.Mcp;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Services.AddHealthChecks();
builder.Services.AddContextDepotApplication(builder.Configuration);
builder.Services.AddContextDepotPersistence(builder.Configuration);
builder.Services
    .AddMcpServer(options => options.ServerInstructions = ContextDepotMcpInstructions.Text)
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<ContextTools>()
    .WithTools<DocumentTools>()
    .WithTools<WorkspaceTools>()
    .WithTools<OwnerTools>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CurrentOwnerBootstrapper>().InitializeAsync();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/readyz");
app.MapMcp("/mcp");

app.Run();

public partial class Program;
