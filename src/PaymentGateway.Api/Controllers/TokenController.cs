using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;  
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.Auth;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers.V1;

[ApiController] 
[Route("api/[controller]")]
public sealed class TokenController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService; 


    public TokenController(
        IJwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }
     
    /// </summary>
    /// This endpoint is only here for testing purposes and for the demo
    /// this is not to be rolled out to production.
    [HttpGet("{id}")] 
    public async Task<ActionResult<string>> GetToken(
        int id)
    {

        return Ok(_jwtTokenService.GenerateToken(id.ToString()));
    }
}