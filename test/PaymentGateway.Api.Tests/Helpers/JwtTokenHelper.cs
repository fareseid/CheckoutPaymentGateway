using Microsoft.Extensions.Options;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Services.Auth;

namespace PaymentGateway.Api.Tests.Helpers
{
    public static class JwtTokenHelper
    {
        public static string Generate(string merchantId = "test-merchant")
        {
            var jwtOptions = new JwtOptions
            {
                Issuer = "payment-gateway",
                Audience = "merchants",
                SecretKey = "test-secret-key-that-is-long-enough-32chars",
                ExpiryMinutes = 60
            };

            var tokenService = new JwtTokenService(
                Options.Create(jwtOptions));

            return tokenService.GenerateToken(merchantId);
        }
    }
}
