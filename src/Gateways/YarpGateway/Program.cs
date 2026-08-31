using System.Text;
using BuildingBlocks.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedLogging();

// JWT auth (same Secret/Issuer/Audience as IdentityService)
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = jwtSection["Secret"];
var jwtIssuer = jwtSection["Issuer"] ?? "SmartFreightLogistics.Identity";
var jwtAudience = jwtSection["Audience"] ?? "SmartFreightLogistics.Gateways";
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jwtSecret) ? "fallback-not-used-32-chars-min-0000" : jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ClientPolicy", p => p.RequireRole("Client"));
    o.AddPolicy("LogisticsManagerPolicy", p => p.RequireRole("LogisticsManager"));
    o.AddPolicy("RPA_Bot_Policy", p => p.RequireRole("RpaBot"));
});

// Add Yarp Reverse Proxy services by reading config from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseHttpsRedirection();  // Keep this only on the Gateway
// One line to activate the Correlation ID middleware at the start of the pipeline
app.UseSharedLogging();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Establish mapping rules for routing incoming client calls straight to target destination microservices
app.MapReverseProxy();

try
{
    Log.Information("Starting API Gateway application layer...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway startup phase terminated unexpectedly");
}
finally
{
    // Guarantee that all buffered diagnostic logs are fully written out before application shuts down
    Log.CloseAndFlush();
}
