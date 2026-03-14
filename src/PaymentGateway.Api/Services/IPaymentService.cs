using Microsoft.Extensions.Hosting;

using PaymentGateway.Api.Domain.Entities;
using PaymentGateway.Api.Domain.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    Task<ProcessPaymentResult> ProcessPaymentAsync(
        string merchantId,
        PostPaymentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<GetPaymentResult?> GetPaymentAsync(
        string merchantId,
        Guid paymentId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessPaymentResult
{
    public Guid PaymentId { get; init; }
    public PaymentStatus Status { get; init; }
    public IReadOnlyList<ValidationError> ValidationErrors { get; init; }
        = Array.Empty<ValidationError>();
}

public sealed class GetPaymentResult
{ 
    public Guid Id { get; init; }
    public PaymentStatus Status { get; set; }
    public string CardNumberLastFour { get; init; } = string.Empty;
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int Amount { get; init; }
     
}
     