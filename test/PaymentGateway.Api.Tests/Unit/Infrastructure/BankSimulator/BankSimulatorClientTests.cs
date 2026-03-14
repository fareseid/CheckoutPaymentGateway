using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Infrastructure.BankSimulator;

namespace PaymentGateway.Api.Tests.Unit.Infrastructure;

public class BankSimulatorClientTests
{
    private readonly BankSimulatorOptions _options = new()
    {
        BaseUrl = "http://localhost:8080",
        TimeoutSeconds = 5,
        CircuitBreakerFailureThreshold = 3,
        CircuitBreakerOpenSeconds = 1
    };

    // -------------------------------------------------------------------------
    // Authorized / Declined
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_BankAuthorizes_ReturnsAuthorizedResponse()
    {
        var handler = FakeHttpHandler.RespondWith(HttpStatusCode.OK, new
        {
            authorized = true,
            authorization_code = "AUTH-001"
        });

        var result = await CreateSut(handler).ProcessPaymentAsync(BuildRequest());

        Assert.NotNull(result);
        Assert.True(result.Authorized);
        Assert.Equal("AUTH-001", result.AuthorizationCode);
    }

    [Fact]
    public async Task ProcessPayment_BankDeclines_ReturnsDeclinedResponse()
    {
        var handler = FakeHttpHandler.RespondWith(HttpStatusCode.OK, new
        {
            authorized = false,
            authorization_code = (string?)null
        });

        var result = await CreateSut(handler).ProcessPaymentAsync(BuildRequest());

        Assert.NotNull(result);
        Assert.False(result.Authorized);
        Assert.Null(result.AuthorizationCode);
    }

    // -------------------------------------------------------------------------
    // Non-success responses
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_BankReturns503_ThrowsHttpRequestException()
    {
        var handler = FakeHttpHandler.AlwaysFail(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateSut(handler).ProcessPaymentAsync(BuildRequest()));
    }

    [Fact]
    public async Task ProcessPayment_BankReturns503_CalledExactlyOnce()
    {
        // No retry — one call, one failure
        var handler = FakeHttpHandler.AlwaysFail(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateSut(handler).ProcessPaymentAsync(BuildRequest()));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_BankReturns400_ThrowsHttpRequestException()
    {
        var handler = FakeHttpHandler.AlwaysFail(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateSut(handler).ProcessPaymentAsync(BuildRequest()));

        Assert.Equal(1, handler.CallCount);
    }

    // -------------------------------------------------------------------------
    // Timeout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_RequestExceedsTimeout_ThrowsHttpRequestException()
    {
        var handler = FakeHttpHandler.DelayedResponse(
            delay: TimeSpan.FromSeconds(_options.TimeoutSeconds + 5),
            statusCode: HttpStatusCode.OK,
            body: new { authorized = true, authorization_code = "AUTH-002" });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateSut(handler).ProcessPaymentAsync(BuildRequest()));
    }

    // -------------------------------------------------------------------------
    // Circuit breaker
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_CircuitBreaker_OpensAfterThresholdFailures()
    {
        var handler = FakeHttpHandler.AlwaysFail(HttpStatusCode.ServiceUnavailable);
        var sut = CreateSut(handler);

        for (var i = 0; i < _options.CircuitBreakerFailureThreshold; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.ProcessPaymentAsync(BuildRequest()));
        }

        var callCountWhenOpen = handler.CallCount;

        // Circuit open — next call should not hit the handler
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProcessPaymentAsync(BuildRequest()));

        Assert.Equal(callCountWhenOpen, handler.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_CircuitBreaker_ClosesAfterRecoveryPeriod()
    {
        var failCount = _options.CircuitBreakerFailureThreshold;
        var handler = FakeHttpHandler.FailThenSucceed(
            failCount: failCount,
            successResponse: new { authorized = true, authorization_code = "AUTH-003" });

        var sut = CreateSut(handler);

        // Trip the circuit
        for (var i = 0; i < failCount; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                sut.ProcessPaymentAsync(BuildRequest()));
        }

        // Wait for recovery
        await Task.Delay(TimeSpan.FromSeconds(_options.CircuitBreakerOpenSeconds + 1));

        // Circuit half-open — probe request goes through
        var result = await sut.ProcessPaymentAsync(BuildRequest());

        Assert.NotNull(result);
        Assert.True(result.Authorized);
    }

    // -------------------------------------------------------------------------
    // Request shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_SendsCorrectJsonBody()
    {
        string? capturedBody = null;
        var handler = FakeHttpHandler.CaptureRequest(
            onRequest: body => capturedBody = body,
            response: new { authorized = true, authorization_code = "AUTH-004" });

        var request = new BankSimulatorRequest
        {
            CardNumber = "2222405343248877",
            ExpiryDate = "04/2030",
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        await CreateSut(handler).ProcessPaymentAsync(request);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.Equal("2222405343248877", doc.RootElement.GetProperty("card_number").GetString());
        Assert.Equal("04/2030", doc.RootElement.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", doc.RootElement.GetProperty("currency").GetString());
        Assert.Equal(100, doc.RootElement.GetProperty("amount").GetInt32());
        Assert.Equal("123", doc.RootElement.GetProperty("cvv").GetString());
    }

    [Fact]
    public async Task ProcessPayment_PostsToCorrectEndpoint()
    {
        string? capturedPath = null;
        var handler = FakeHttpHandler.CaptureRequest(
            onRequest: _ => { },
            response: new { authorized = true, authorization_code = "AUTH-005" },
            onPath: path => capturedPath = path);

        await CreateSut(handler).ProcessPaymentAsync(BuildRequest());

        Assert.Equal("/payments", capturedPath);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IBankSimulatorClient CreateSut(FakeHttpHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };

        return new BankSimulatorClient(
            httpClient,
            Options.Create(_options),
            NullLogger<BankSimulatorClient>.Instance);
    }

    private static BankSimulatorRequest BuildRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryDate = "04/2030",
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };
}

public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
    private int _callCount;

    public int CallCount => _callCount;

    private FakeHttpHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return await _handler(request, cancellationToken);
    }
    public static FakeHttpHandler RespondWith(HttpStatusCode statusCode, object body) =>
    new((_, _) => Task.FromResult(JsonResponse(statusCode, body)));

    public static FakeHttpHandler AlwaysFail(HttpStatusCode statusCode) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    public static FakeHttpHandler FailThenSucceed(int failCount, object successResponse)
    {
        var calls = 0;
        return new((_, _) =>
        {
            calls++;
            return calls <= failCount
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
                : Task.FromResult(JsonResponse(HttpStatusCode.OK, successResponse));
        });
    }

    public static FakeHttpHandler DelayedResponse(
        TimeSpan delay,
        HttpStatusCode statusCode,
        object body) =>
        new(async (_, ct) =>
        {
            await Task.Delay(delay, ct);
            return JsonResponse(statusCode, body);
        });

    public static FakeHttpHandler CaptureRequest(
        Action<string> onRequest,
        object response,
        Action<string>? onPath = null) =>
        new(async (req, _) =>
        {
            onPath?.Invoke(req.RequestUri?.AbsolutePath ?? string.Empty);
            var body = req.Content is not null
                ? await req.Content.ReadAsStringAsync()
                : string.Empty;
            onRequest(body);
            return JsonResponse(HttpStatusCode.OK, response);
        });

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object body)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        return response;
    }
}