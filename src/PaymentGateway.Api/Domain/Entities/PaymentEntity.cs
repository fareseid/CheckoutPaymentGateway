using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Domain.Entities;

/// <summary>
/// The persisted record of a payment attempt.
/// Full card number is NEVER stored — only the last four digits.
/// </summary>
public sealed class PaymentEntity
{
    /// <summary>Unique payment identifier (GUID).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();


    /// <summary>
    /// The merchant who submitted this payment. 
    /// </summary>
    public string MerchantId { get; init; } = string.Empty;

    /// <summary>Idempotency key supplied by the merchant on creation.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Outcome: Authorized, Declined, or Rejected.</summary>
    public PaymentStatus Status { get; set; }

    /// <summary>Last four digits of the card number only.</summary>
    public string CardNumberLastFour { get; init; } = string.Empty;

    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }

    /// <summary>ISO 4217 currency code, e.g. GBP, USD, EUR.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Amount in minor currency units (e.g. 1050 = $10.50).</summary>
    public int Amount { get; init; }

    /// <summary>Authorization code returned by the bank. Null if not authorized.</summary>
    public string? AuthorizationCode { get; set; }

    /// <summary>UTC timestamp of when the payment was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}