using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Infrastructure.Repositories;
namespace PaymentGateway.Api.Tests.Helpers;

public static class WebApplicationFactoryExtensionsTests
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

    public static WebApplicationFactory<TProgram> WithService<TProgram, TService>(
        this WebApplicationFactory<TProgram> factory,
        TService implementation)
        where TProgram : class
        where TService : class
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(TService));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddSingleton(implementation);
            });
        });
    }

    public static HttpClient CreateClient(this WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient();
    }

    internal static HttpClient WithAuthHeader(this HttpClient client)
    {
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + JwtTokenHelperTests.Generate());
        return client;
    }

    internal static HttpClient WithHMAC(this HttpClient client, string signature)
    {
        client.DefaultRequestHeaders.Add("X-HMAC-Signature", signature); 
        return client;
    }
}