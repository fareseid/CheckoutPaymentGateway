using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    public void Add(PostPaymentResponse payment);
    public PostPaymentResponse Get(Guid id);
}