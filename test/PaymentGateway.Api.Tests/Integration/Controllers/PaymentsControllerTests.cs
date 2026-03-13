using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.Helpers;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Random _random = new(); 
    private readonly WebApplicationFactory<Program> _factory;
    public PaymentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999).ToString(),
            Currency = "GBP"
        };

        var repo = new PaymentsRepository();
        repo.Add(payment);

        var client = _factory.WithJwtSettings().WithRepository(repo).CreateClient().WithAuthHeader();
        var response = await client.GetAsync($"/api/v1/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        var client = _factory.WithJwtSettings().WithRepository().CreateClient().WithAuthHeader();
        var response = await client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task Returns401IfRequestUnauthorized()
    {
        var client = _factory.WithJwtSettings().WithRepository().CreateClient();
        var response = await client.GetAsync($"/api/v1/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    } 
}