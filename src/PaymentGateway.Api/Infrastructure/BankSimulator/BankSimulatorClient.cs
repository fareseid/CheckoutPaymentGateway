// Infrastructure/BankSimulator/BankSimulatorClient.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Infrastructure.Resilience;

namespace PaymentGateway.Api.Infrastructure.BankSimulator;

/// <summary>
/// HTTP client for the acquiring bank simulator.
///
/// Resilience layers:
///   1. Timeout         — cancels the request if the bank takes too long
///   2. Circuit breaker — fails fast after N consecutive failures,
///                        preventing thundering herd on a struggling bank
///
/// Intentionally no retry — payment requests are not idempotent at the
/// bank level. Retrying risks a double charge if the bank received the
/// original request but the response was lost in transit.
/// The caller (PaymentService) handles failure by persisting Failed status
/// and letting the merchant decide whether to resubmit.
/// </summary>
public sealed class BankSimulatorClient : IBankSimulatorClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly BankSimulatorOptions _options;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger<BankSimulatorClient> _logger;

    public BankSimulatorClient(
        HttpClient httpClient,
        IOptions<BankSimulatorOptions> options,
        ILogger<BankSimulatorClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _circuitBreaker = new CircuitBreaker(
            _options.CircuitBreakerFailureThreshold,
            _options.CircuitBreakerOpenSeconds);
    }

    public async Task<BankSimulatorResponse?> ProcessPaymentAsync(
        BankSimulatorRequest request,
        CancellationToken cancellationToken = default)
    {
        // ---------------------------------------------------------------
        // Circuit breaker — fail fast if circuit is open
        // ---------------------------------------------------------------
        if (!_circuitBreaker.IsRequestAllowed())
        {
            _logger.LogWarning("Circuit breaker is open — bank request rejected immediately.");
            throw new HttpRequestException(
                "Circuit breaker is open. Bank simulator is unavailable.");
        }

        // ---------------------------------------------------------------
        // Per-request timeout via linked cancellation token
        // ---------------------------------------------------------------
        using var timeoutCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/payments", request, JsonOptions, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync<BankSimulatorResponse>(
                        JsonOptions, timeoutCts.Token);

                _circuitBreaker.OnSuccess();

                _logger.LogInformation(
                    "Bank responded. Authorized: {Authorized}",
                    result?.Authorized);

                return result;
            }

            // Non-success — record failure and surface to caller
            _circuitBreaker.OnFailure();

            _logger.LogError(
                "Bank returned non-success status {StatusCode}.",
                response.StatusCode);

            throw new HttpRequestException(
                $"Bank returned {(int)response.StatusCode} {response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out — not cancelled by the caller
            _circuitBreaker.OnFailure();

            _logger.LogError(
                "Bank request timed out after {TimeoutSeconds}s.",
                _options.TimeoutSeconds);

            throw new HttpRequestException(
                $"Bank request timed out after {_options.TimeoutSeconds}s.");
        }
    }
}