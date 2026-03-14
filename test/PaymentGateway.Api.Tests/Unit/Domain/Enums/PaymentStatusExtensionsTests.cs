using PaymentGateway.Api.Domain.Enums;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Tests.Unit.Domain.Enums;

public class PaymentRecordStatusExtensionsTests
{
    [Theory]
    [InlineData(PaymentRecordStatus.Authorized, PaymentStatus.Authorized)]
    [InlineData(PaymentRecordStatus.Declined, PaymentStatus.Declined)]
    [InlineData(PaymentRecordStatus.Failed, PaymentStatus.Rejected)]
    [InlineData(PaymentRecordStatus.Processing, PaymentStatus.Rejected)]
    [InlineData(PaymentRecordStatus.Rejected, PaymentStatus.Rejected)]
    public void ToApiStatus_MapsCorrectly(
        PaymentRecordStatus domainStatus,
        PaymentStatus expectedApiStatus)
    {
        var result = domainStatus.ToApiStatus();

        Assert.Equal(expectedApiStatus, result);
    }

    [Fact]
    public void ToApiStatus_AllDomainValuesAreMapped()
    {
        // Ensures no new PaymentRecordStatus value is added without
        // a corresponding mapping — throws ArgumentOutOfRangeException if missed.
        var allDomainStatuses = Enum.GetValues<PaymentRecordStatus>();

        foreach (var status in allDomainStatuses)
        {
            var exception = Record.Exception(() => status.ToApiStatus());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void ToApiStatus_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        var invalidStatus = (PaymentRecordStatus)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => invalidStatus.ToApiStatus());
    }
}