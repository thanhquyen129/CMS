using LCMS.Api.Auth;
using LCMS.Api.Endpoints;
using LCMS.Api.Middleware;
using LCMS.Application;
using LCMS.Application.Currencies;
using LCMS.Application.Demo;
using LCMS.Infrastructure;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "cms-api")
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "cms-api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new RenderedCompactJsonFormatter()),
        preserveStaticLogger: true);

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.Configure<RateLimitingOptions>(
        builder.Configuration.GetSection(RateLimitingOptions.SectionName));
    builder.Services.AddLcmsAuth(builder.Configuration, builder.Environment);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpMetrics();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<TenantResolutionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    await MigrateDatabaseAsync(app);
    await BootstrapUserSeeder.EnsureAsync(app.Services);
    await SeedDemoIfEnabledAsync(app);

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "ok",
        service = "cms-api",
        utc = DateTime.UtcNow
    })).AllowAnonymous();

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
    }).AllowAnonymous();

    // Prometheus text exposition + HTTP request metrics (UseHttpMetrics).
    app.MapMetrics().AllowAnonymous();

    app.MapGet("/", () => Results.Ok(new
    {
        product = "Cost Management System",
        shortName = "CMS",
        message = "LCMS API — Clean Architecture (TD1)"
    })).AllowAnonymous();

    // Dev token always Development-only inside handlers; seed-demo also when Demo:AllowEndpoint.
    app.MapDevAuthEndpoints();

    app.MapAuthEndpoints();
    app.MapTenantBillEndpoints();
    app.MapIdentityEndpoints();
    app.MapMasterDataEndpoints();
    app.MapOperationalReferenceEndpoints();
    app.MapRatePricingEndpoints();
    app.MapCostEndpoints();
    app.MapRevenueEndpoints();
    app.MapFinancialDocumentEndpoints();
    app.MapExposureApArEndpoints();
    app.MapSettlementEndpoints();
    app.MapFinancialControlEndpoints();
    app.MapBankFeedEndpoints();
    app.MapFinancialCloseEndpoints();
    app.MapDashboardReportingEndpoints();
    app.MapAuditIntegrationEndpoints();
    app.MapTerminologyEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

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
    await CurrencyCatalogSeeder.EnsureBaselineAsync(db);
}

static async Task SeedDemoIfEnabledAsync(WebApplication app)
{
    var demo = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
    if (!demo.SeedOnStartup)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    await seeder.EnsureAsync();
}

public partial class Program;
