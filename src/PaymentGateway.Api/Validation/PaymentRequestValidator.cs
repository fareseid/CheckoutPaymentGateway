using System.Text.RegularExpressions;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validation;

/// <summary>
/// Validates an incoming PostPaymentRequest against all business rules.
/// All rules run on every call — never short-circuits — so the merchant
/// always receives the complete list of problems in a single response.
/// </summary>
public sealed class PaymentRequestValidator : IValidator<PostPaymentRequest>
{
    // Only these three currencies are supported in this version of the gateway.
    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.Ordinal) { "GBP", "USD", "EUR" };

    // Card number: 14–19 digits, numeric only.
    private static readonly Regex CardNumberRegex =
        new(@"^\d{14,19}$", RegexOptions.Compiled);

    // CVV: 3–4 digits, numeric only.
    private static readonly Regex CvvRegex =
        new(@"^\d{3,4}$", RegexOptions.Compiled);

    public ValidationResult Validate(PostPaymentRequest request)
    {
        var errors = new List<ValidationError>();

        ValidateCardNumber(request.CardNumber, errors);
        ValidateExpiryMonth(request.ExpiryMonth, errors);
        ValidateExpiryDate(request.ExpiryMonth, request.ExpiryYear, errors);
        ValidateCurrency(request.Currency, errors);
        ValidateAmount(request.Amount, errors);
        ValidateCvv(request.Cvv, errors);

        return errors.Count == 0
            ? ValidationResult.Valid
            : new ValidationResult(errors);
    }

    // -------------------------------------------------------------------------
    // Private rule methods — one method per field, easy to read and test
    // -------------------------------------------------------------------------

    private static void ValidateCardNumber(string? cardNumber, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            errors.Add(new ValidationError("CardNumber", "Card number is required."));
            return; // no point running regex on null/empty
        }

        if (!CardNumberRegex.IsMatch(cardNumber))
        {
            errors.Add(new ValidationError(
                "CardNumber",
                "Card number must be between 14 and 19 numeric digits with no spaces or separators."));
        }
    }

    private static void ValidateExpiryMonth(int expiryMonth, List<ValidationError> errors)
    {
        if (expiryMonth < 1 || expiryMonth > 12)
        {
            errors.Add(new ValidationError(
                "ExpiryMonth",
                "Expiry month must be between 1 and 12."));
        }
    }

    private static void ValidateExpiryDate(int expiryMonth, int expiryYear, List<ValidationError> errors)
    {
        var now = DateTime.UtcNow;

        // Year alone already in the past
        if (expiryYear < now.Year)
        {
            errors.Add(new ValidationError(
                "ExpiryYear",
                "Card has expired. Expiry year is in the past."));
            return;
        }

        // Same year but month already passed
        if (expiryYear == now.Year && expiryMonth < now.Month)
        {
            errors.Add(new ValidationError(
                "ExpiryYear",
                "Card has expired. Expiry month and year combination is in the past."));
        }

        // Note: a card expiring this exact month is still valid.
    }

    private static void ValidateCurrency(string? currency, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            errors.Add(new ValidationError("Currency", "Currency is required."));
            return;
        }

        if (!SupportedCurrencies.Contains(currency))
        {
            errors.Add(new ValidationError(
                "Currency",
                $"Currency '{currency}' is not supported. Supported currencies: GBP, USD, EUR."));
        }
    }

    private static void ValidateAmount(int amount, List<ValidationError> errors)
    {
        if (amount <= 0)
        {
            errors.Add(new ValidationError(
                "Amount",
                "Amount must be a positive integer representing the value in minor currency units."));
        }
    }

    private static void ValidateCvv(string? cvv, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(cvv))
        {
            errors.Add(new ValidationError("Cvv", "CVV is required."));
            return;
        }

        if (!CvvRegex.IsMatch(cvv))
        {
            errors.Add(new ValidationError(
                "Cvv",
                "CVV must be 3 or 4 numeric digits."));
        }
    }
}