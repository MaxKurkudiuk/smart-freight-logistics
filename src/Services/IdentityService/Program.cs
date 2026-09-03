using BuildingBlocks.Logging;
using IdentityService.Data;
using IdentityService.Extensions;
using IdentityService.Services;

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

var app = builder.Build();

app.UseSharedLogging();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // Seed default users (dev hardcoded) — must run before handling requests
    await IdentitySeeder.SeedAsync(app.Services);
    // Configure the HTTP request pipeline.
    app.MapOpenApi();
}

app.Run();
