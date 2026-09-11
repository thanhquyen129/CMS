using LCMS.Api.Endpoints;
using LCMS.Api.Middleware;
using LCMS.Application;
using LCMS.Infrastructure;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await MigrateDatabaseAsync(app);

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "cms-api",
    utc = DateTime.UtcNow
}));

app.MapGet("/ready", async (LcmsDbContext db, CancellationToken ct) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        if (!canConnect)
        {
            return Results.Json(new
            {
                status = "not_ready",
                service = "cms-api",
                database = "unreachable",
                utc = DateTime.UtcNow
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            status = "ready",
            service = "cms-api",
            database = "ok",
            utc = DateTime.UtcNow
        });
    }
    catch (Exception)
    {
        return Results.Json(new
        {
            status = "not_ready",
            service = "cms-api",
            database = "error",
            utc = DateTime.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/", () => Results.Ok(new
{
    product = "Cost Management System",
    shortName = "CMS",
    message = "LCMS API — Clean Architecture (TD1)"
}));

app.MapTenantBillEndpoints();

app.Run();

static async Task MigrateDatabaseAsync(WebApplication app)
{
    var migrate = app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment());
    if (!migrate)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
    await db.Database.MigrateAsync();
}

public partial class Program;
