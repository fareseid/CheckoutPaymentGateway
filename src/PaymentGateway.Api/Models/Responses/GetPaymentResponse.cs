namespace PaymentGateway.Api.Models.Responses;

/// <summary>
/// Response returned when a merchant retrieves a previously created payment.
/// Identical shape to PostPaymentResponse — kept separate so they can diverge.
/// </summary>
public sealed class GetPaymentResponse
{
    public Guid Id { get; init; }
    public PaymentStatus Status { get; init; }
    public string CardNumberLastFour { get; init; } = string.Empty;
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int Amount { get; init; } 
}