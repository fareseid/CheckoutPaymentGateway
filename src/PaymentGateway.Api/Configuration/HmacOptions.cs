namespace PaymentGateway.Api.Configuration;

/// <summary>Bound from appsettings: "Hmac" section.</summary>
public sealed class HmacOptions
{
    public const string SectionName = "Hmac";

    /// <summary>
    /// Shared secret used to compute and verify HMAC-SHA256 signatures.
    /// This should be added in a vault or secrets manager in production, not hardcoded or checked into source control.
    /// But for this exercise, we'll just read it from appsettings.json for simplicity.
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;
}