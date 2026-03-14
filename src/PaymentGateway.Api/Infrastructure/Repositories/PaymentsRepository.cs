
using PaymentGateway.Api.Domain.Entities;

namespace PaymentGateway.Api.Infrastructure.Repositories
{
    public class PaymentsRepository : IPaymentsRepository
    {
        public void Add(PaymentEntity entity)
        {
            throw new NotImplementedException();
        }

        public PaymentEntity? Get(Guid id, string merchantId)
        {
            throw new NotImplementedException();
        }

        public PaymentEntity? GetByIdempotencyKey(string key, string merchantId)
        {
            throw new NotImplementedException();
        }

        public bool Update(PaymentEntity entity, int expectedRowVersion)
        {
            throw new NotImplementedException();
        }
    }
}
