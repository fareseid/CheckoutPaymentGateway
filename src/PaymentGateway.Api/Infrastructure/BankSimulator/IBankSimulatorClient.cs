namespace PaymentGateway.Api.Infrastructure.BankSimulator;

public interface IBankSimulatorClient
{
    Task<BankSimulatorResponse?> ProcessPaymentAsync(
        BankSimulatorRequest request,
        CancellationToken cancellationToken = default);
}