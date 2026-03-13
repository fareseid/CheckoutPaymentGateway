namespace PaymentGateway.Api.Configuration;

/// <summary>Bound from appsettings: "BankSimulator" section.</summary>
public sealed class BankSimulatorOptions
{
    public const string SectionName = "BankSimulator";

    /// <summary>Base URL of the acquiring bank simulator, e.g. http://localhost:8080</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Number of retry attempts on transient failures (5xx, timeout).</summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>Delay in milliseconds between retries.</summary>
    public int RetryDelayMs { get; init; } = 200;

    /// <summary>
    /// Number of consecutive failures before the circuit opens.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>Seconds the circuit stays open before moving to half-open.</summary>
    public int CircuitBreakerOpenSeconds { get; init; } = 30;
}