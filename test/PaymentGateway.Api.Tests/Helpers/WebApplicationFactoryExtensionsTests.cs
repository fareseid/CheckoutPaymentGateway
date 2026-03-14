using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

using PaymentGateway.Api.Infrastructure.BankSimulator;
using PaymentGateway.Api.Infrastructure.Repositories;
using PaymentGateway.Api.Models.Requests;
namespace PaymentGateway.Api.Tests.Helpers;

public static class WebApplicationFactoryExtensionsTests
{

    public static WebApplicationFactory<Program> WithHmacSettings(this WebApplicationFactory<Program> factory)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Hmac:SecretKey"] = "test-secret-key-that-is-long-enough-32chars"
                };
                config.AddInMemoryCollection(settings!);
            });
        });
    }

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
    public static WebApplicationFactory<Program> WithBankSimulatorClient(this WebApplicationFactory<Program> factory, IBankSimulatorClient bankSimulatorClient)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IBankSimulatorClient));
                if (descriptor != null)
                    services.Remove(descriptor);
                services.AddSingleton<IBankSimulatorClient>(
                    bankSimulatorClient);
            });
        });
    }

    public static WebApplicationFactory<Program> WithPaymentsRepository(this WebApplicationFactory<Program> factory, IPaymentsRepository repository )
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
                    repository);
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

    internal static HttpClient WithIdempotencyKey(this HttpClient client,string key)
    {
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        return client;
    }

    internal static HttpClient WithAuthHeader(this HttpClient client)
    {
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + JwtTokenHelper.Generate());
        return client;
    }

    internal static HttpClient WithValidHMAC(this HttpClient client, IPaymentRequest request)
    {
        client.DefaultRequestHeaders.Add("X-HMAC-Signature", HMACHelper.Generate(request));
        return client;
    }

    internal static HttpClient WithHMAC(this HttpClient client, string signature)
    {
        client.DefaultRequestHeaders.Add("X-HMAC-Signature", signature); 
        return client;
    }
}