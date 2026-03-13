namespace PaymentGateway.Api.Services.Auth;

public interface IJwtTokenService
{
    string GenerateToken(string merchantId); 
}

public sealed class TokenValidationResult
{
    public bool IsValid { get; init; }
    public System.Security.Claims.ClaimsPrincipal? Principal { get; init; }
}