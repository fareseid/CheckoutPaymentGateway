using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests.Unit.Validation;

public class PaymentRequestValidatorTests
{
    private readonly IValidator<PostPaymentRequest> _validator = new PaymentRequestValidator();

    // -------------------------------------------------------------------------
    // Card number
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var result = _validator.Validate(BuildValidRequest());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_MissingCardNumber_ReturnsError(string? cardNumber)
    {
        var result = _validator.Validate(BuildValidRequest(cardNumber: cardNumber!));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "CardNumber");
    }

    [Theory]
    [InlineData("1234567890123")]          // 13 digits — too short
    [InlineData("12345678901234567890")]   // 20 digits — too long
    public void Validate_CardNumberWrongLength_ReturnsError(string cardNumber)
    {
        var result = _validator.Validate(BuildValidRequest(cardNumber: cardNumber));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "CardNumber");
    }

    [Theory]
    [InlineData("4111111111111ABC")]   // contains letters
    [InlineData("4111-1111-1111-111")] // contains dashes
    [InlineData("4111 1111 1111 111")] // contains spaces
    public void Validate_CardNumberNonNumeric_ReturnsError(string cardNumber)
    {
        var result = _validator.Validate(BuildValidRequest(cardNumber: cardNumber));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "CardNumber");
    }

    [Theory]
    [InlineData("41111111111111")]        // 14 digits — min boundary
    [InlineData("4111111111111111")]      // 16 digits — typical Visa
    [InlineData("1234567890123456789")]   // 19 digits — max boundary
    public void Validate_CardNumberValidLength_ReturnsValid(string cardNumber)
    {
        var result = _validator.Validate(BuildValidRequest(cardNumber: cardNumber));
        Assert.True(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // Expiry month
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]   // below min
    [InlineData(13)]  // above max
    [InlineData(-1)]  // negative
    public void Validate_ExpiryMonthOutOfRange_ReturnsError(int month)
    {
        var result = _validator.Validate(BuildValidRequest(expiryMonth: month));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ExpiryMonth");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Validate_ExpiryMonthInRange_ReturnsValid(int month)
    {
        var result = _validator.Validate(BuildValidRequest(expiryMonth: month));
        Assert.True(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // Expiry year
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ExpiryYearInPast_ReturnsError()
    {
        var result = _validator.Validate(BuildValidRequest(expiryYear: DateTime.UtcNow.Year - 1));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ExpiryYear");
    }

    [Fact]
    public void Validate_ExpiryMonthAndYearInPast_ReturnsError()
    {
        // Current month last year — definitely expired
        var result = _validator.Validate(BuildValidRequest(
            expiryMonth: DateTime.UtcNow.Month,
            expiryYear: DateTime.UtcNow.Year - 1));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ExpiryYear");
    }

    [Fact]
    public void Validate_ExpiryIsCurrentMonthAndYear_ReturnsValid()
    {
        // A card expiring this month is still valid
        var result = _validator.Validate(BuildValidRequest(
            expiryMonth: DateTime.UtcNow.Month,
            expiryYear: DateTime.UtcNow.Year));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ExpiryYearInFuture_ReturnsValid()
    {
        var result = _validator.Validate(BuildValidRequest(expiryYear: DateTime.UtcNow.Year + 2));
        Assert.True(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // Currency
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_MissingCurrency_ReturnsError(string? currency)
    {
        var result = _validator.Validate(BuildValidRequest(currency: currency!));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Currency");
    }

    [Theory]
    [InlineData("GB")]    // too short
    [InlineData("GBPP")]  // too long
    [InlineData("gbp")]   // lowercase
    [InlineData("JPY")]   // valid ISO but not in our whitelist
    [InlineData("XYZ")]   // not a real currency
    public void Validate_UnsupportedCurrency_ReturnsError(string currency)
    {
        var result = _validator.Validate(BuildValidRequest(currency: currency));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Currency");
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Validate_SupportedCurrency_ReturnsValid(string currency)
    {
        var result = _validator.Validate(BuildValidRequest(currency: currency));
        Assert.True(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // Amount
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]    // zero is not a valid payment
    [InlineData(-1)]   // negative
    [InlineData(-100)]
    public void Validate_AmountNotPositive_ReturnsError(int amount)
    {
        var result = _validator.Validate(BuildValidRequest(amount: amount));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Amount");
    }

    [Theory]
    [InlineData(1)]      // 1 minor unit — minimum valid
    [InlineData(1050)]   // $10.50
    [InlineData(100000)]
    public void Validate_AmountPositive_ReturnsValid(int amount)
    {
        var result = _validator.Validate(BuildValidRequest(amount: amount));
        Assert.True(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // CVV
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_MissingCvv_ReturnsError(string? cvv)
    {
        var result = _validator.Validate(BuildValidRequest(cvv: cvv!));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Cvv");
    }

    [Theory]
    [InlineData("12")]      // too short
    [InlineData("12345")]   // too long
    public void Validate_CvvWrongLength_ReturnsError(string cvv)
    {
        var result = _validator.Validate(BuildValidRequest(cvv: cvv));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Cvv");
    }

    [Theory]
    [InlineData("12A")]   // letters
    [InlineData("AB3")]
    public void Validate_CvvNonNumeric_ReturnsError(string cvv)
    {
        var result = _validator.Validate(BuildValidRequest(cvv: cvv));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Cvv");
    }

    [Theory]
    [InlineData("123")]   // 3 digits — standard
    [InlineData("1234")]  // 4 digits — Amex
    public void Validate_CvvValid_ReturnsValid(string cvv)
    {
        var result = _validator.Validate(BuildValidRequest(cvv: cvv));
        Assert.True(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // Multiple errors at once
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_MultipleInvalidFields_ReturnsAllErrors()
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "invalid",
            ExpiryMonth = 0,
            ExpiryYear = 2000,
            Currency = "XYZ",
            Amount = -1,
            Cvv = "ab"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "CardNumber");
        Assert.Contains(result.Errors, e => e.Field == "ExpiryMonth");
        Assert.Contains(result.Errors, e => e.Field == "ExpiryYear");
        Assert.Contains(result.Errors, e => e.Field == "Currency");
        Assert.Contains(result.Errors, e => e.Field == "Amount");
        Assert.Contains(result.Errors, e => e.Field == "Cvv");
    }

    // -------------------------------------------------------------------------
    // Builder helper — all defaults produce a valid request
    // -------------------------------------------------------------------------

    private static PostPaymentRequest BuildValidRequest(
        string cardNumber = "2222405343248877",
        int expiryMonth = 4,
        int expiryYear = 2030,
        string currency = "GBP",
        int amount = 100,
        string cvv = "123") => new()
        {
            CardNumber = cardNumber,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = currency,
            Amount = amount,
            Cvv = cvv
        };
}