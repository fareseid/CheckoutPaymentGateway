using System.IdentityModel.Tokens.Jwt;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Services.Auth;

namespace PaymentGateway.Api.Tests.Unit.Services.Auth;

public class JwtTokenServiceTests
{
    private readonly JwtOptions _options = new()
    {
        Issuer = "payment-gateway",
        Audience = "merchants",
        SecretKey = "test-secret-key-that-is-long-enough-32chars",
        ExpiryMinutes = 60
    };

    private readonly IJwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        _sut = new JwtTokenService(Options.Create(_options));
    }

    [Fact]
    public void GenerateToken_ValidMerchantId_ReturnsNonEmptyToken()
    {
        var token = _sut.GenerateToken("merchant-123");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateToken_ReturnsParsableJwt()
    {
        var token = _sut.GenerateToken("merchant-123");

        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));

        var parsed = handler.ReadJwtToken(token);
        Assert.Equal(_options.Issuer, parsed.Issuer);
    }

    [Fact]
    public void GenerateToken_ContainsMerchantIdClaim()
    {
        var merchantId = "merchant-abc";
        var token = _sut.GenerateToken(merchantId);

        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        var claim = parsed.Claims.FirstOrDefault(c => c.Type == "merchant_id");
        Assert.NotNull(claim);
        Assert.Equal(merchantId, claim.Value);
    }

    [Fact]
    public void GenerateToken_HasCorrectExpiry()
    {
        var before = DateTime.UtcNow;
        var token = _sut.GenerateToken("merchant-123");
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        var expectedExpiryMin = before.AddMinutes(_options.ExpiryMinutes);
        var expectedExpiryMax = after.AddMinutes(_options.ExpiryMinutes);

        var tolerance = TimeSpan.FromSeconds(1);

        Assert.True(parsed.ValidTo >= expectedExpiryMin.Subtract(tolerance));
        Assert.True(parsed.ValidTo <= expectedExpiryMax.Add(tolerance));
    }
     

    [Fact]
    public void GenerateToken_TwoCallsSameMerchant_ReturnsDifferentTokens()
    {
        // Tokens should differ due to issued-at time even for same merchant
        var token1 = _sut.GenerateToken("merchant-123");
        var token2 = _sut.GenerateToken("merchant-123");

        Assert.NotEqual(token1, token2);
    }
}