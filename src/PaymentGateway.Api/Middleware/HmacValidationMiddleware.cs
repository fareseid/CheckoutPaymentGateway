using System.Text;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Services.Auth;

namespace PaymentGateway.Api.Middleware;

/// <summary>
/// Validates the HMAC-SHA256 signature on mutating requests (POST, PUT, PATCH).
/// The merchant must include an X-HMAC-Signature header whose value is the
/// HMAC-SHA256 hex digest of the raw request body, signed with the shared secret.
///
/// GET / HEAD / OPTIONS are exempt — they carry no body to sign.
/// </summary>
public sealed class HmacValidationMiddleware
{
    private static readonly HashSet<string> ProtectedMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH" };

    private readonly RequestDelegate _next;
    private readonly IHmacSignatureService _hmacService;
    private readonly string _secret;

    public HmacValidationMiddleware(
        RequestDelegate next,
        IHmacSignatureService hmacService,
        IOptions<HmacOptions> options)
    {
        _next = next;
        _hmacService = hmacService;
        _secret = options.Value.SecretKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ProtectedMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // EnableBuffering allows the body to be read multiple times.
        // Without this, reading the body here would consume it and the
        // controller would receive an empty stream.
        context.Request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(
            context.Request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))   // leaveOpen — do not close the underlying stream
        {
            body = await reader.ReadToEndAsync();
        }

        // Reset so the controller can read from the beginning
        context.Request.Body.Position = 0;

        var signature = context.Request.Headers["X-HMAC-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) ||
            !_hmacService.ValidateSignature(body, _secret, signature))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"code":"invalid_hmac","message":"Request signature is missing or invalid."}""");
            return;
        }

        await _next(context);
    }
}