using Serilog;
using ContextDepot.Infrastructure.Persistence;
using ContextDepot.Infrastructure.CurrentOwner;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Services.AddHealthChecks();
builder.Services.AddContextDepotApplication(builder.Configuration);
builder.Services.AddContextDepotPersistence(builder.Configuration);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CurrentOwnerBootstrapper>().InitializeAsync();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/readyz");

app.Run();

public partial class Program;
