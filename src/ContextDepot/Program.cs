using Serilog;
using ContextDepot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Services.AddHealthChecks();
builder.Services.AddContextDepotPersistence(builder.Configuration);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/readyz");

app.Run();

public partial class Program;
