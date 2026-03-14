
using PaymentGateway.Api.Domain.Entities;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Infrastructure.Repositories
{
    public class PaymentsRepository : IPaymentsRepository
    {
        private readonly List<PaymentEntity> _payments = new();
        public void Add(PaymentEntity entity)
        {
            _payments.Add(entity);
        }

        public PaymentEntity? Get(Guid id, string merchantId)
        {
           return _payments.FirstOrDefault(p => p.Id == id && p.MerchantId == merchantId);
        }

        public PaymentEntity? GetByIdempotencyKey(string key, string merchantId)
        {
            return _payments.FirstOrDefault(p => p.IdempotencyKey== key && p.MerchantId == merchantId);
        }

        public bool Update(PaymentEntity entity)
        {
            // Optimistic Concurrency, if the row was already updated before getting here then the update will fail
            // we can catch the dbupdateconcurrency exception and return a 409 conflict to the client
            var index = _payments.FindIndex(p => p.Id == entity.Id);

            if (index == -1)
                return false;
             
            _payments[index] = entity;
            return true;
        }
    }
}
