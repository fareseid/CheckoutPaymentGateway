using PaymentGateway.Api.Domain.Entities;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    Task<ProcessPaymentResult> ProcessPaymentAsync(
        string merchantId,
        PostPaymentRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PaymentEntity?> GetPaymentAsync(
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