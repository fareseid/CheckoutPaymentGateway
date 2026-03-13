using System.Net;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestPlatform.TestHost;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services.Auth;
using PaymentGateway.Api.Tests.Helpers;

namespace PaymentGateway.Api.Tests.Integration;

public class HmacMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private readonly HmacOptions _hmacOptions = new()
    {
        SecretKey = "test-hmac-secret-key-for-integration"
    };

    public HmacMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    } 

    [Fact]
    public async Task PostPayment_WithNoHmacHeader_Returns401()
    {
        var client = _factory.WithJwtSettings().CreateClient().WithAuthHeader();
        var request = BuildValidRequest();
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
         

        var response = await client.PostAsync("/api/v1/payments", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_WithInvalidHmac_Returns401()
    { 
        var request = BuildValidRequest();
        var (content, _) = SignRequest(request);
        var client = _factory.WithJwtSettings().CreateClient().WithAuthHeader().WithHMAC("invalidsignature");
         

        var response = await client.PostAsync("/api/v1/payments", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_DoesNotRequireHmac()
    {
        // GET requests are read-only — HMAC only protects mutating operations
        var client = _factory.WithJwtSettings().CreateClient().WithAuthHeader(); 

        var response = await client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        // 404 is fine — proves middleware didn't block it with 401
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PostPaymentRequest BuildValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private (StringContent content, string signature) SignRequest(PostPaymentRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var service = new HmacSignatureService();
        var signature = service.ComputeSignature(json, _hmacOptions.SecretKey);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return (content, signature);
    }
}