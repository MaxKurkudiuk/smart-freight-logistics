namespace IdentityService.Options;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SmartFreightLogistics.Identity";
    public string Audience { get; set; } = "SmartFreightLogistics.Gateways";
    public int ExpiryInMinutes { get; set; } = 60;
}
