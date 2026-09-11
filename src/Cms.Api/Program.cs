var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
    message = "CMS API bootstrap"
}));

app.Run();

public partial class Program;
