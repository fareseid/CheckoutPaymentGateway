using PaymentGateway.Api.Services.Auth;

namespace PaymentGateway.Api.Tests.Unit.Services.Auth;

public class HmacSignatureServiceTests
{
    private const string Secret = "test-hmac-secret-key";
    private readonly IHmacSignatureService _sut = new HmacSignatureService();

    [Fact]
    public void ComputeSignature_ValidPayload_ReturnsNonEmptyString()
    {
        var signature = _sut.ComputeSignature("""{"amount":100}""", Secret);

        Assert.NotNull(signature);
        Assert.NotEmpty(signature);
    }

    [Fact]
    public void ComputeSignature_SamePayloadAndSecret_ReturnsSameSignature()
    {
        var payload = """{"amount":100,"currency":"GBP"}""";

        var sig1 = _sut.ComputeSignature(payload, Secret);
        var sig2 = _sut.ComputeSignature(payload, Secret);

        Assert.Equal(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentPayload_ReturnsDifferentSignature()
    {
        var sig1 = _sut.ComputeSignature("""{"amount":100}""", Secret);
        var sig2 = _sut.ComputeSignature("""{"amount":200}""", Secret);

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentSecret_ReturnsDifferentSignature()
    {
        var payload = """{"amount":100}""";

        var sig1 = _sut.ComputeSignature(payload, "secret-one");
        var sig2 = _sut.ComputeSignature(payload, "secret-two");

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void ValidateSignature_CorrectSignature_ReturnsTrue()
    {
        var payload = """{"amount":100,"currency":"GBP"}""";
        var signature = _sut.ComputeSignature(payload, Secret);

        var isValid = _sut.ValidateSignature(payload, Secret, signature);

        Assert.True(isValid);
    }

    [Fact]
    public void ValidateSignature_WrongSecret_ReturnsFalse()
    {
        var payload = """{"amount":100}""";
        var signature = _sut.ComputeSignature(payload, Secret);

        var isValid = _sut.ValidateSignature(payload, "wrong-secret", signature);

        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_TamperedPayload_ReturnsFalse()
    {
        var payload = """{"amount":100}""";
        var signature = _sut.ComputeSignature(payload, Secret);

        var isValid = _sut.ValidateSignature("""{"amount":999}""", Secret, signature);

        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_TamperedSignature_ReturnsFalse()
    {
        var payload = """{"amount":100}""";
        var signature = _sut.ComputeSignature(payload, Secret);
        var tampered = signature[..^4] + "XXXX";

        var isValid = _sut.ValidateSignature(payload, Secret, tampered);

        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_EmptySignature_ReturnsFalse()
    {
        var payload = """{"amount":100}""";

        var isValid = _sut.ValidateSignature(payload, Secret, string.Empty);

        Assert.False(isValid);
    }

    [Fact]
    public void ValidateSignature_UsesConstantTimeComparison()
    {
        // Both calls should take similar time — no early exit on mismatch.
        // We can't precisely measure this in a unit test, but we can verify
        // the method doesn't throw and returns false for a wrong signature
        // regardless of where the difference is.
        var payload = """{"amount":100}""";
        var correct = _sut.ComputeSignature(payload, Secret);

        // Differ at start
        var wrongStart = "AAAA" + correct[4..];
        // Differ at end
        var wrongEnd = correct[..^4] + "AAAA";

        Assert.False(_sut.ValidateSignature(payload, Secret, wrongStart));
        Assert.False(_sut.ValidateSignature(payload, Secret, wrongEnd));
    }
}