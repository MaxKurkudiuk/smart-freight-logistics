using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationService.Clients;

/// <summary>
/// 4.8 HttpClient + RpaBot JWT (HS256 same Secret/Issuer/Audience) + Polly → OrderService PUT Customs
/// </summary>
public sealed class OrderStatusClient : IOrderStatusClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderStatusClient> _logger;

    public OrderStatusClient(HttpClient http, IConfiguration config, ILogger<OrderStatusClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task MarkCustomsAsync(Guid orderId, CancellationToken ct = default)
    {
        _logger.LogInformation("OrderStatus MarkCustoms OrderId={OrderId}", orderId);

        var token = GenerateRpaBotToken();
        var request = new HttpRequestMessage(HttpMethod.Put, $"api/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new { newStatus = 3, notes = "RPA customs submitted" }) // 3 = Customs
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Order {OrderId} marked Customs via OrderService", orderId);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("Failed to mark Customs for OrderId={OrderId} Status={Status} Body={Body}",
            orderId, response.StatusCode, body);
        response.EnsureSuccessStatusCode();
    }

    private string GenerateRpaBotToken()
    {
        var secret = _config["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret not configured for RpaBot token");
        if (secret.Length < 32)
            throw new InvalidOperationException("JwtSettings:Secret must be >=32 chars");

        var issuer = _config["JwtSettings:Issuer"] ?? "SmartFreightLogistics.Identity";
        var audience = _config["JwtSettings:Audience"] ?? "SmartFreightLogistics.Gateways";
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(5);

        // RpaBot identity — fixed Guid for IntegrationService (could be from config)
        var rpaBotId = _config["RpaBot:UserId"] ?? "22222222-2222-2222-2222-222222222222";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, rpaBotId),
            new Claim(ClaimTypes.NameIdentifier, rpaBotId),
            new Claim(ClaimTypes.Role, "RpaBot"),
            new Claim(JwtRegisteredClaimNames.Email, "rpa@example.com"),
            new Claim(ClaimTypes.Name, "RPA Bot"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
