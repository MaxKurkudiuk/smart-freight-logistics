using BuildingBlocks.Logging;
using IdentityService.Data;
using IdentityService.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddPasswordHasher(builder.Configuration);

var baseConnectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

var dbPassword = builder.Configuration["DatabaseSettings:Password"];

var connectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);
if (!string.IsNullOrWhiteSpace(dbPassword))
{
    connectionBuilder.Password = dbPassword;
}

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionBuilder.ConnectionString));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseSharedLogging();

app.UseRouting();
//app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
