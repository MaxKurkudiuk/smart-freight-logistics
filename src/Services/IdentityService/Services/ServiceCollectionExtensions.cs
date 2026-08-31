using System.Text;
using IdentityService.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PBKDF2 password hasher as singleton (stateless) + binds PasswordHasherOptions.
    /// Call from Program.cs: builder.Services.AddPasswordHasher(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddPasswordHasher(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PasswordHasherOptions>(configuration.GetSection(PasswordHasherOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        return services;
    }

    public static IServiceCollection AddJwtGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        return services;
    }

    public static IServiceCollection AddIdentityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind JwtSettings for auth (already bound by AddJwtGenerator, but configure again is safe)
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        var secret = jwtSection["Secret"];
        var issuer = jwtSection["Issuer"] ?? "SmartFreightLogistics.Identity";
        var audience = jwtSection["Audience"] ?? "SmartFreightLogistics.Gateways";

        // Fail-fast if secret missing is allowed to happen at runtime in JwtTokenGenerator;
        // for auth we still configure with placeholder — validation will fail until proper secret via Development.json/env.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(secret) ? "fallback-not-used-32-chars-min-0000" : secret));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy("ClientPolicy", p => p.RequireRole("Client"));
            o.AddPolicy("LogisticsManagerPolicy", p => p.RequireRole("LogisticsManager"));
            o.AddPolicy("RPA_Bot_Policy", p => p.RequireRole("RpaBot"));
        });

        return services;
    }
}
