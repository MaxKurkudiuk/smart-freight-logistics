using BuildingBlocks.Logging;
using IdentityService.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var baseConnectionString = builder.Configuration.GetConnectionString("IdentityDb");
var dbPassword = builder.Configuration["DatabaseSettings:Password"];

var connectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
{
    Password = dbPassword
};

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(connectionBuilder.ConnectionString)));

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
