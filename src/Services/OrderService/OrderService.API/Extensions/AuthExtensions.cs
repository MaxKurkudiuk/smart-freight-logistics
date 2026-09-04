using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace OrderService.API.Extensions;

public static class AuthExtensions
{
    public static WebApplicationBuilder AddOrderAuth(this WebApplicationBuilder builder)
    {
        builder.AddJwtAuthentication();
        builder.AddOrderAuthorization();
        return builder;
    }

    public static WebApplicationBuilder AddJwtAuthentication(this WebApplicationBuilder builder)
    {
        var jwtSection = builder.Configuration.GetSection("JwtSettings");
        var jwtSecret = jwtSection["Secret"];
        var jwtIssuer = jwtSection["Issuer"] ?? "SmartFreightLogistics.Identity";
        var jwtAudience = jwtSection["Audience"] ?? "SmartFreightLogistics.Gateways";
        var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            string.IsNullOrWhiteSpace(jwtSecret) ? "fallback-not-used-32-chars-min-0000" : jwtSecret));

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

        return builder;
    }

    public static WebApplicationBuilder AddOrderAuthorization(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("ClientPolicy", p => p.RequireRole("Client"))
            .AddPolicy("LogisticsManagerPolicy", p => p.RequireRole("LogisticsManager"))
            .AddPolicy("RPA_Bot_Policy", p => p.RequireRole("RpaBot"));

        return builder;
    }
}
