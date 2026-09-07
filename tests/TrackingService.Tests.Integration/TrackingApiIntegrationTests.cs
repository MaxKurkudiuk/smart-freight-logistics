using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.Redis;
using Xunit;

namespace TrackingService.Tests.Integration;

public sealed class TrackingApiIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, true)
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private static string GenerateToken(string role = "Client")
    {
        var secret = JwtHelperSecret;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var userId = Guid.NewGuid().ToString();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: "SmartFreightLogistics.Identity",
            audience: "SmartFreightLogistics.Gateways",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private const string JwtHelperSecret = "test-secret-must-be-at-least-32-chars-1234567890AB";

    public async ValueTask InitializeAsync()
    {
        await _redis.StartAsync();

        var redisConnection = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";

        Environment.SetEnvironmentVariable("JwtSettings__Secret", JwtHelperSecret);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", "SmartFreightLogistics.Identity");
        Environment.SetEnvironmentVariable("JwtSettings__Audience", "SmartFreightLogistics.Gateways");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", redisConnection);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:Secret"] = JwtHelperSecret,
                    ["JwtSettings:Issuer"] = "SmartFreightLogistics.Identity",
                    ["JwtSettings:Audience"] = "SmartFreightLogistics.Gateways",
                    ["ConnectionStrings:Redis"] = redisConnection
                });
            });
            builder.UseSetting("ConnectionStrings:Redis", redisConnection);
        });

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Health_ShouldReturn200()
    {
        var res = await _client.GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Put_WithoutToken_ShouldReturn401()
    {
        var id = Guid.NewGuid();
        var res = await _client.PutAsJsonAsync($"/api/tracking/{id}", new { latitude = 50.45, longitude = 30.52 });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_WithInvalidLat_ShouldReturn400()
    {
        var id = Guid.NewGuid();
        var token = GenerateToken("Client");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.PutAsJsonAsync($"/api/tracking/{id}", new { latitude = 100, longitude = 30.52 });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_And_Get_ShouldReturnTracking_CacheAside()
    {
        var id = Guid.NewGuid();
        var token = GenerateToken("Client");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var put = await _client.PutAsJsonAsync($"/api/tracking/{id}", new { latitude = 50.45, longitude = 30.52, speedKmh = 60, notes = "Kyiv depot" });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await put.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().NotBeNull();

        var get = await _client.GetAsync($"/api/tracking/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await get.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        getBody.Should().NotBeNull();

        // Second GET hits cache (same result)
        var get2 = await _client.GetAsync($"/api/tracking/{id}");
        get2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_NotFound_ShouldReturn404()
    {
        var token = GenerateToken("Client");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.GetAsync($"/api/tracking/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
