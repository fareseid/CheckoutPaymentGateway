namespace PaymentGateway.Api.Models.Requests;

/// <summary>
/// Incoming payment request from a merchant.
/// The full card number is accepted here but never stored.
/// </summary>
public sealed record PostPaymentRequest
{
    /// <summary>
    /// Full card number, 14–19 numeric digits.
    /// The gateway stores only the last four.
    /// </summary>
    public string CardNumber { get; init; } = string.Empty;

    /// <summary>Expiry month, 1–12.</summary>
    public int ExpiryMonth { get; init; }

    /// <summary>Expiry year, four digits, must be in the future.</summary>
    public int ExpiryYear { get; init; }

    /// <summary>ISO 4217 currency code (GBP, USD, or EUR supported).</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Amount in minor currency units. Must be a positive integer.</summary>
    public int Amount { get; init; }

    /// <summary>Card CVV, 3–4 numeric digits.</summary>
    public string Cvv { get; init; } = string.Empty;
}