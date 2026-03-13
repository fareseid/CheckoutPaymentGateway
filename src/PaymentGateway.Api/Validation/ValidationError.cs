namespace PaymentGateway.Api.Validation;

/// <summary>
/// A single field-level validation failure.
/// </summary>
public sealed class ValidationError
{
    public string Field { get; }
    public string Message { get; }

    public ValidationError(string field, string message)
    {
        Field = field;
        Message = message;
    }
}