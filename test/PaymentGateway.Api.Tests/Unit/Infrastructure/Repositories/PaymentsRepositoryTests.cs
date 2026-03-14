using PaymentGateway.Api.Domain.Entities; 
using PaymentGateway.Api.Infrastructure.Repositories;
using PaymentGateway.Api.Models; 

namespace PaymentGateway.Api.Tests.Unit.Infrastructure.Repositories;

public class PaymentsRepositoryTests
{
    private readonly IPaymentsRepository _sut = new PaymentsRepository();

    // -------------------------------------------------------------------------
    // Add + Get
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_ThenGet_ReturnsPayment()
    {
        var entity = BuildEntity();

        _sut.Add(entity);
        var result = _sut.Get(entity.Id, entity.MerchantId);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result.Id);
    }

    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        var result = _sut.Get(Guid.NewGuid(), "merchant-123");

        Assert.Null(result);
    }

    [Fact]
    public void Get_WrongMerchantId_ReturnsNull()
    {
        // Merchant A cannot retrieve merchant B's payment
        var entity = BuildEntity(merchantId: "merchant-a");
        _sut.Add(entity);

        var result = _sut.Get(entity.Id, "merchant-b");

        Assert.Null(result);
    }

    [Fact]
    public void Get_CorrectMerchantId_ReturnsPayment()
    {
        var entity = BuildEntity(merchantId: "merchant-a");
        _sut.Add(entity);

        var result = _sut.Get(entity.Id, "merchant-a");

        Assert.NotNull(result);
    }

    // -------------------------------------------------------------------------
    // Idempotency key lookup
    // -------------------------------------------------------------------------

    [Fact]
    public void GetByIdempotencyKey_ExistingKey_ReturnsPayment()
    {
        var entity = BuildEntity(idempotencyKey: "key-abc");
        _sut.Add(entity);

        var result = _sut.GetByIdempotencyKey("key-abc", entity.MerchantId);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result.Id);
    }

    [Fact]
    public void GetByIdempotencyKey_UnknownKey_ReturnsNull()
    {
        var result = _sut.GetByIdempotencyKey("unknown-key", "merchant-123");

        Assert.Null(result);
    }

    [Fact]
    public void GetByIdempotencyKey_WrongMerchant_ReturnsNull()
    {
        // Idempotency keys are scoped per merchant
        var entity = BuildEntity(idempotencyKey: "key-abc", merchantId: "merchant-a");
        _sut.Add(entity);

        var result = _sut.GetByIdempotencyKey("key-abc", "merchant-b");

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Update (optimistic concurrency)
    // -------------------------------------------------------------------------

    [Fact]
    public void Update_CorrectRowVersion_Succeeds()
    {
        var entity = BuildEntity();
        _sut.Add(entity);

        entity.Status = PaymentStatus.Authorized;
        var success = _sut.Update(entity, expectedRowVersion: 1);

        Assert.True(success);
    }

    [Fact]
    public void Update_CorrectRowVersion_IncrementsRowVersion()
    {
        var entity = BuildEntity();
        _sut.Add(entity);

        _sut.Update(entity, expectedRowVersion: 1);

        var updated = _sut.Get(entity.Id, entity.MerchantId);
        Assert.Equal(2, updated!.RowVersion);
    }

    [Fact]
    public void Update_StaleRowVersion_ReturnsFalse()
    {
        var entity = BuildEntity();
        _sut.Add(entity);

        // Simulate concurrent update — row version is now 2
        _sut.Update(entity, expectedRowVersion: 1);

        // Second caller still holds version 1 — should be rejected
        var staleSuccess = _sut.Update(entity, expectedRowVersion: 1);
        Assert.False(staleSuccess);
    }

    [Fact]
    public void Update_UnknownPayment_ReturnsFalse()
    {
        var entity = BuildEntity();

        var success = _sut.Update(entity, expectedRowVersion: 1);

        Assert.False(success);
    }

    // -------------------------------------------------------------------------
    // Builder
    // -------------------------------------------------------------------------

    private static PaymentEntity BuildEntity(
        string merchantId = "merchant-123",
        string? idempotencyKey = null) => new()
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            IdempotencyKey = idempotencyKey,
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "1234",
            ExpiryMonth = 6,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 1050,
            RowVersion = 1
        };
}