using System.Security.Cryptography;
using System.Text;

namespace PaymentGateway.Api.Services.Auth;

/// <summary>
/// Computes and validates HMAC-SHA256 signatures.
/// Uses constant-time comparison to prevent timing attacks.
/// </summary>
public sealed class HmacSignatureService : IHmacSignatureService
{
    public string ComputeSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        // Hex string — lowercase, no dashes
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool ValidateSignature(string payload, string secret, string signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        var expected = ComputeSignature(payload, secret);

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(signature);

        // Constant-time comparison — never short-circuits on mismatch.
        // Prevents timing attacks where an attacker probes byte-by-byte.
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}