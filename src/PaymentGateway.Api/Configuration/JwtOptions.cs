namespace PaymentGateway.Api.Configuration;

/// <summary>Bound from appsettings: "Jwt" section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// This should be added in a vault or secrets manager in production, not hardcoded or checked into source control.
    /// But for this exercise, we'll just read it from appsettings.json for simplicity.
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Token lifetime in minutes.</summary>
    public int ExpiryMinutes { get; init; } = 60;
}