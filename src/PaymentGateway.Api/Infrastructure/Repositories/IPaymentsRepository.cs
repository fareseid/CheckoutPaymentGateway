using PaymentGateway.Api.Domain.Entities;

namespace PaymentGateway.Api.Infrastructure.Repositories;

public interface IPaymentsRepository
{
    void Add(PaymentEntity entity);
    PaymentEntity? Get(Guid id, string merchantId);
    PaymentEntity? GetByIdempotencyKey(string key, string merchantId);
    bool Update(PaymentEntity entity);
}