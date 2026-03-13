namespace PaymentGateway.Api.Validation;

/// <summary>
/// Outcome of a validation pass — carries all errors found in a single run.
/// Never short-circuits so the caller always gets the full error list.
/// </summary>
public sealed class ValidationResult
{
    public static readonly ValidationResult Valid = new(Array.Empty<ValidationError>());

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }
}