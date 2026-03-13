namespace PaymentGateway.Api.Models.Responses;

/// <summary>
/// Uniform error envelope returned for all non-2xx responses.
/// Merchants can always expect this shape on failures.
/// </summary>
public sealed class ApiErrorResponse
{
    /// <summary>Short machine-readable error code, e.g. "validation_failed".</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Field-level validation errors. Empty for non-validation errors.
    /// Key = field name, Value = list of error messages for that field.
    /// </summary>
    public Dictionary<string, string[]> Errors { get; init; } = new();

    /// <summary>UTC timestamp of when the error occurred.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Correlation ID echoed from the request for tracing.</summary>
    public string? TraceId { get; init; }
}