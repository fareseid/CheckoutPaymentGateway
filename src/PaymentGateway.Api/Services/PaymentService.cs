
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Domain.Entities;
using PaymentGateway.Api.Infrastructure.BankSimulator;
using PaymentGateway.Api.Infrastructure.Repositories;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services
{
    public class PaymentService : IPaymentService
    {
        IPaymentsRepository _repository;
        public PaymentService(IPaymentsRepository repository, IBankSimulatorClient bankClient, PaymentRequestValidator paymentRequestValidator, IMemoryCache cache,NullLogger<PaymentService> instance) {
            _repository = repository;
        }

        public Task<PaymentEntity?> GetPaymentAsync(string merchantId, Guid paymentId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProcessPaymentResult> ProcessPaymentAsync(string merchantId, PostPaymentRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
