using System;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Controllers.V1;

using Xunit;

namespace PaymentGateway.Api.Tests.Unit.Controllers;

public class ApiControllerBaseTests
{
    private sealed class TestController : ApiControllerBase
    {
        public string GetMerchantIdForTest() => MerchantId;
    }

    [Fact]
    public void MerchantId_ReturnsValue_FromClaims()
    {
        // Arrange
        var controller = new TestController();
        var claims = new[] { new Claim("merchant_id", "merchant-123") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var merchantId = controller.GetMerchantIdForTest();

        // Assert
        Assert.Equal("merchant-123", merchantId);
    }

    [Fact]
    public void MerchantId_ThrowsInvalidOperationException_IfClaimMissing()
    {
        // Arrange
        var controller = new TestController();
        var identity = new ClaimsIdentity(); // no claims
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => controller.GetMerchantIdForTest());
    }
}