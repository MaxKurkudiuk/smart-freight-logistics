using BuildingBlocks.Logging;
using IdentityService.Data;
using IdentityService.Extensions;
using IdentityService.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddPasswordHasher(builder.Configuration);
builder.Services.AddJwtGenerator(builder.Configuration);
builder.Services.AddIdentityAuth(builder.Configuration);
builder.Services.AddControllers();

builder.AddDbContext();

// 7.2 HealthChecks — postgres readiness + self liveness
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.GetIdentityDbConnectionString(), name: "postgres", tags: new[] { "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

var app = builder.Build();

app.UseSharedLogging();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 7.2 health endpoints — /health (all), /health/ready (deps), /health/live (self)
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready"), ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live"), ResponseWriter = WriteHealthResponse });

if (app.Environment.IsDevelopment())
{
    // Seed default users (dev hardcoded) — must run before handling requests
    await IdentitySeeder.SeedAsync(app.Services);
    // Configure the HTTP request pipeline.
    app.MapOpenApi();
}

app.Run();

static async Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
    });
}
