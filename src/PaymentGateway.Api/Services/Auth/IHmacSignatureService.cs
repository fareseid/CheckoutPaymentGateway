namespace PaymentGateway.Api.Services.Auth;

public interface IHmacSignatureService
{
    /// <summary>Computes a HMAC-SHA256 signature for the given payload.</summary>
    string ComputeSignature(string payload, string secret);

    /// <summary>
    /// Validates a signature using constant-time comparison
    /// to prevent timing attacks.
    /// </summary>
    bool ValidateSignature(string payload, string secret, string signature);
}