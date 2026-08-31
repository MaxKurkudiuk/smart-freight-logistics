using IdentityService.Options;

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
}
