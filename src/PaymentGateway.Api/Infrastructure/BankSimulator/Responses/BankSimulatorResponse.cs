using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.BankSimulator;

/// <summary>
/// Response received from the acquiring bank simulator.
/// </summary>
public sealed class BankSimulatorResponse
{
    [JsonPropertyName("authorized")]
    public bool Authorized { get; init; }

    /// <summary>
    /// Present only when Authorized = true.
    /// Null on declined responses.
    /// </summary>
    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}