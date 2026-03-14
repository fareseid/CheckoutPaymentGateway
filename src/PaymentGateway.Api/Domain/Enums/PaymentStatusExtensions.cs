namespace PaymentGateway.Api.Domain.Enums;

public static class PaymentStatusExtensions
{
    /// <summary>
    /// Maps the internal domain status to the API-facing status.
    /// Processing and Failed are internal — both surface as Rejected
    /// to the merchant since neither resulted in a payment.
    /// </summary>
    public static Models.PaymentStatus ToApiStatus(this PaymentRecordStatus status) =>
        status switch
        {
            PaymentRecordStatus.Authorized => Models.PaymentStatus.Authorized,
            PaymentRecordStatus.Declined => Models.PaymentStatus.Declined,

            // Processing should never reach the API — it means the service
            // returned before the bank call completed, which is a bug.
            // We surface it as Rejected and log a warning in the service.
            PaymentRecordStatus.Processing => Models.PaymentStatus.Rejected,
            PaymentRecordStatus.Failed => Models.PaymentStatus.Rejected,
            PaymentRecordStatus.Rejected => Models.PaymentStatus.Rejected,

            _ => throw new ArgumentOutOfRangeException(nameof(status), status,
                     "Unmapped domain payment status.")
        };
}