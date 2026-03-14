 namespace PaymentGateway.Api.Models.Responses;

/// <summary>
/// Response returned to the merchant after a payment attempt that
/// reached the acquiring bank (Authorized or Declined).
/// </summary>
public sealed class PostPaymentResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;

    /// <summary>Last four digits of the card number only.</summary>
    public string CardNumberLastFour { get; init; } = string.Empty;

    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int Amount { get; init; } 
}