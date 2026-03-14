namespace PaymentGateway.Api.Configuration;

/// <summary>Bound from appsettings: "BankSimulator" section.</summary>
public sealed class BankSimulatorOptions
{

    public const string SectionName = "BankSimulator";
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 10;
    public int CircuitBreakerFailureThreshold { get; init; } = 5;
    public int CircuitBreakerOpenSeconds { get; init; } = 30;
}