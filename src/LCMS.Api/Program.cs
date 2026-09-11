using LCMS.Api.Middleware;
using LCMS.Application;
using LCMS.Infrastructure;

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

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "cms-api",
    utc = DateTime.UtcNow
}));

app.MapGet("/ready", () => Results.Ok(new
{
    status = "ready",
    service = "cms-api",
    utc = DateTime.UtcNow
}));

app.MapGet("/", () => Results.Ok(new
{
    product = "Cost Management System",
    shortName = "CMS",
    message = "LCMS API — Clean Architecture (TD1)"
}));

app.Run();

public partial class Program;
