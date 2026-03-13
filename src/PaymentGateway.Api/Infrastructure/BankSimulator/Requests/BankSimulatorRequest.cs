using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.BankSimulator;

/// <summary>
/// Payload sent to the acquiring bank simulator.
/// Property names use snake_case to match the simulator's JSON contract.
/// </summary>
public sealed class BankSimulatorRequest
{
    [JsonPropertyName("card_number")]
    public string CardNumber { get; init; } = string.Empty;

    /// <summary>Format: MM/YYYY</summary>
    [JsonPropertyName("expiry_date")]
    public string ExpiryDate { get; init; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("cvv")]
    public string Cvv { get; init; } = string.Empty;
}