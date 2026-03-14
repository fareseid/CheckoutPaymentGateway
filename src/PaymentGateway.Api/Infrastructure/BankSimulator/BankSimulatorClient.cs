
namespace PaymentGateway.Api.Infrastructure.BankSimulator;

public class BankSimulatorClient : IBankSimulatorClient
{
    public Task<BankSimulatorResponse?> ProcessPaymentAsync(BankSimulatorRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}