using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.OpenApi.Validations;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.Helpers;
using PaymentGateway.Api.Tests.Unit.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Random _random = new(); 
    private readonly WebApplicationFactory<Program> _factory; 
    public PaymentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory; 
    }



    // -------------------------------------------------------------------------
    // GET /api/v1/payments/{id}
    // -------------------------------------------------------------------------

    [Fact] 
    public async Task RetrievesAPaymentSuccessfully()
    {
        var paymentId = Guid.NewGuid();
        var expectedPayment = new GetPaymentResult
        {
            Id = paymentId,
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999).ToString(),
            Currency = "GBP"
        };


        var fake = new FakePaymentService();
        fake.SetupGetPayment(expectedPayment);

        var client = _factory
            .WithService<Program, IPaymentService>(fake)
            .WithJwtSettings()
            .CreateClient()
            .WithAuthHeader();

        var response = await client.GetAsync($"/api/v1/payments/{paymentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        var fake = new FakePaymentService();
        fake.SetupGetPayment(null);

        var client = _factory
            .WithService<Program, IPaymentService>(fake)
            .WithJwtSettings()
            .CreateClient()
            .WithAuthHeader(); 
        var response = await client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task Returns401IfRequestUnauthorized()
    {
        var client = _factory.WithJwtSettings().CreateClient();
        var response = await client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    // -------------------------------------------------------------------------
    // POST /api/v1/payments
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostPayment_ValidRequest_BankAuthorizes_Returns200()
    { 
        var authorizedBankSimulatorResponse = new FakeBankSimulatorClient
        {
            NextResponse = new Infrastructure.BankSimulator.BankSimulatorResponse
            {
                Authorized = true,
                AuthorizationCode = "authorization_code"
            }
        };
        var fakeValidator = new FakePaymentRequestValidator();

        PostPaymentRequest request = BuildValidRequest();

        var client = _factory
            .WithService<Program, IValidator<PostPaymentRequest>>(fakeValidator)
            .WithBankSimulatorClient(authorizedBankSimulatorResponse) 
            .WithHmacSettings()
            .WithJwtSettings()
            .CreateClient()
            .WithAuthHeader()
            .WithIdempotencyKey(Guid.NewGuid().ToString())
            .WithValidHMAC(request);

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json"); 

        var response = await client.PostAsync("/api/v1/payments", content);
        var body = await DeserializeAsync<PostPaymentResponse>(response);

        Assert.True(fakeValidator.WasCalled);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal(PaymentStatus.Authorized, body.Status);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task PostPayment_ValidRequest_BankDeclines_Returns200()
    {
        var notAuthorizedBankSimulatorResponse = new FakeBankSimulatorClient
        {
            NextResponse = new Infrastructure.BankSimulator.BankSimulatorResponse
            {
                Authorized = false,
                AuthorizationCode = null
            }
        };

        PostPaymentRequest request = BuildValidRequest();

        var client = _factory 
          .WithBankSimulatorClient(notAuthorizedBankSimulatorResponse)
          .WithHmacSettings()
          .WithJwtSettings()
          .CreateClient()
          .WithAuthHeader()
          .WithIdempotencyKey(Guid.NewGuid().ToString())
          .WithValidHMAC(request);


        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/payments", content);
        var body = await DeserializeAsync<PostPaymentResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Declined, body.Status);
    }

    [Fact]
    public async Task PostPayment_NullIdempotencyKey_Returns400()
    {
        PostPaymentRequest request = BuildValidRequest();

        var client = _factory 
          .WithHmacSettings()
          .WithJwtSettings()
          .CreateClient()
          .WithAuthHeader() 
          .WithValidHMAC(request);


        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/payments", content);
        var body = await DeserializeAsync<ApiErrorResponse>(response);


        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", body!.Code);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task PostPayment_InvalidRequest_Returns400WithFieldErrors()
    { 
        var request = BuildValidRequest() with { CardNumber = "invalid", Cvv = "ab" };
        var client = _factory 
          .WithHmacSettings()
          .WithJwtSettings()
          .CreateClient()
          .WithAuthHeader()
          .WithIdempotencyKey(Guid.NewGuid().ToString())
          .WithValidHMAC(request);

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/payments", content);
        var body = await DeserializeAsync<ApiErrorResponse>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CardNumber", body!.Errors.Keys);
        Assert.Contains("Cvv", body.Errors.Keys);
    }

    [Fact]
    public async Task PostPayment_ValidRequest_ReturnsLastFourDigitsOnly()
    {
        var request = BuildValidRequest() with { CardNumber = "2222405343248877" };
        var client = _factory
          .WithHmacSettings()
          .WithJwtSettings()
          .CreateClient()
          .WithAuthHeader()
          .WithIdempotencyKey(Guid.NewGuid().ToString())
          .WithValidHMAC(request);

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");


        var response = await client.PostAsync("/api/v1/payments", content);
        var body = await DeserializeAsync<PostPaymentResponse>(response);

        Assert.Equal("8877", body!.CardNumberLastFour);
    }

    [Fact]
    public async Task PostPayment_NoAuthToken_Returns401()
    {
        PostPaymentRequest request = BuildValidRequest();

        var client = _factory 
            .WithJwtSettings() 
            .CreateClient();

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/payments", content); 
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_NoHmacHeader_Returns401()
    {
        PostPaymentRequest request = BuildValidRequest();

        var client = _factory 
            .WithJwtSettings()
            .CreateClient()
            .WithAuthHeader()
            .WithIdempotencyKey(Guid.NewGuid().ToString());

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/payments", content);
        var body = await DeserializeAsync<PostPaymentResponse>(response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_InvalidHmac_Returns401()
    {

        PostPaymentRequest request = BuildValidRequest();

        var client = _factory
            .WithJwtSettings()
            .WithHmacSettings()
            .CreateClient()
            .WithAuthHeader()
            .WithIdempotencyKey(Guid.NewGuid().ToString())
            .WithHMAC("invalid-hmac");

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/payments", content);
        var body = await DeserializeAsync<PostPaymentResponse>(response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task PostPayment_SameIdempotencyKey_Returns200BothTimes()
    {

        var authorizedBankSimulatorResponse = new FakeBankSimulatorClient
        {
            NextResponse = new Infrastructure.BankSimulator.BankSimulatorResponse
            {
                Authorized = true,
                AuthorizationCode = "authorization_code"
            }
        }; 

        PostPaymentRequest request = BuildValidRequest();
 
        var client = _factory 
            .WithBankSimulatorClient(authorizedBankSimulatorResponse)
            .WithHmacSettings()
            .WithJwtSettings()
            .CreateClient()
            .WithAuthHeader()
            .WithIdempotencyKey(Guid.NewGuid().ToString())
            .WithValidHMAC(request);

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response1 = await client.PostAsync("/api/v1/payments", content);
        var response2 = await client.PostAsync("/api/v1/payments", content); 
         
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }
     

    private static PostPaymentRequest BuildValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }


    public class FakePaymentRequestValidator : IValidator<PostPaymentRequest>
    {
        public bool WasCalled { get; private set; }
        public ValidationResult ResultToReturn { get; set; } = ValidationResult.Valid; // valid by default

        public ValidationResult Validate(PostPaymentRequest instance)
        {
            WasCalled = true;
            return ResultToReturn;
        }

        // stub out the rest of the interface
        public ValidationResult Validate(IValidationContext context) => ResultToReturn;
        public Task<ValidationResult> ValidateAsync(PostPaymentRequest instance, CancellationToken ct = default)
            => Task.FromResult(ResultToReturn);
        public Task<ValidationResult> ValidateAsync(IValidationContext context, CancellationToken ct = default)
            => Task.FromResult(ResultToReturn); 
        public bool CanValidateInstancesOfType(Type type) => type == typeof(PostPaymentRequest);
    }

}

public class FakePaymentService : IPaymentService
{
    private GetPaymentResult? _paymentToReturn;

    public void SetupGetPayment(GetPaymentResult? payment) => _paymentToReturn = payment;

    public Task<GetPaymentResult?> GetPaymentAsync(
        string merchantId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_paymentToReturn);

    public Task<ProcessPaymentResult> ProcessPaymentAsync(
        string merchantId,
        PostPaymentRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}