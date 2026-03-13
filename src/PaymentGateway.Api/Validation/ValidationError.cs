namespace PaymentGateway.Api.Validation
{
    public sealed class ValidationError
    {
        public string Field { get; }
        public string Message { get; }
    }
}
