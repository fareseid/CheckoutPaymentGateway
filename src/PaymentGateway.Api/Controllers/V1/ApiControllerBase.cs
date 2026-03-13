using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Extracts the merchant ID from the validated JWT claims.
    /// Never null on authenticated requests — [Authorize] ensures
    /// the token was valid before the controller action runs.
    /// </summary>
    protected string MerchantId =>
        User.FindFirstValue("merchant_id")
        ?? throw new InvalidOperationException(
            "merchant_id claim missing from token. This should never happen on an authenticated request.");
}