using System.Text.Json;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services.Auth;

namespace PaymentGateway.Api.Tests.Helpers
{
    public static class HMACHelper
    {
        private static readonly HmacOptions _hmacOptions = new()
        {
            SecretKey = "test-secret-key-that-is-long-enough-32chars"
        };

        public static string Generate(IPaymentRequest request)
        {

            var json = JsonSerializer.Serialize((object)request);
            var service = new HmacSignatureService();
            var signature = service.ComputeSignature(json, _hmacOptions.SecretKey);
            return signature;
        }
    }
}
