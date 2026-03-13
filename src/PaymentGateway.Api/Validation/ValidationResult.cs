namespace PaymentGateway.Api.Validation
{
    public sealed class ValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<ValidationError> Errors { get; }
    }

}
