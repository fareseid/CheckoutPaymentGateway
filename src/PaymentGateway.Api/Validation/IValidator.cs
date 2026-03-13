namespace PaymentGateway.Api.Validation;

/// <summary>
/// Generic synchronous validator contract.
/// Keeps validation logic completely decoupled from controllers and services.
/// </summary>
public interface IValidator<T>
{
    ValidationResult Validate(T request);
}