using System.Net.Http.Headers;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Services;
namespace PaymentGateway.Api.Tests.Helpers;

public static class TestWebApplicationFactoryExtensions
{

    public static WebApplicationFactory<Program> WithJwtSettings(this WebApplicationFactory<Program> factory)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "payment-gateway",
                    ["Jwt:Audience"] = "merchants",
                    ["Jwt:SecretKey"] = "test-secret-key-that-is-long-enough-32chars"
                };
                config.AddInMemoryCollection(settings!);
            });
        });
    }

    public static WebApplicationFactory<Program> WithRepository(this WebApplicationFactory<Program> factory, IPaymentsRepository? repository = null)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IPaymentsRepository));
                if (descriptor != null)
                    services.Remove(descriptor);
                services.AddSingleton<IPaymentsRepository>(
                    repository ?? new PaymentsRepository());
            });
        });
    }
    public static HttpClient CreateClient(this WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient();
    }

    internal static HttpClient WithAuthHeader(this HttpClient client)
    {
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + TestJwtTokenHelper.Generate());
        return client;
    }

    internal static HttpClient WithHMAC(this HttpClient client, string signature)
    {
        client.DefaultRequestHeaders.Add("X-HMAC-Signature", signature); 
        return client;
    }
}