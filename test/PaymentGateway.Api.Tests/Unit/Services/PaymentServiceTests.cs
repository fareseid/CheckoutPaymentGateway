// test/PaymentGateway.Api.Tests/Unit/Services/PaymentServiceTests.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Domain.Entities; 
using PaymentGateway.Api.Infrastructure.BankSimulator;
using PaymentGateway.Api.Infrastructure.Repositories;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests.Unit.Services;

public class PaymentServiceTests
{
    private readonly FakePaymentsRepository _repository = new();
    private readonly FakeBankSimulatorClient _bankClient = new();
    private readonly IMemoryCache _cache;
    private readonly IPaymentService _sut;

    public PaymentServiceTests()
    {
        _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        _sut = new PaymentService(
            _repository,
            _bankClient,
            new PaymentRequestValidator(),
            _cache,
            NullLogger<PaymentService>.Instance);
    }

    // -------------------------------------------------------------------------
    // ProcessPayment — validation failures (Rejected)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_InvalidRequest_ReturnsRejected()
    {
        var request = BuildValidRequest() with { CardNumber = "invalid" };

        var result = await _sut.ProcessPaymentAsync("merchant-123", request, "idem-key-1");

        Assert.Equal(PaymentStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task ProcessPayment_InvalidRequest_DoesNotCallBank()
    {
        var request = BuildValidRequest() with { Amount = -1 };

        await _sut.ProcessPaymentAsync("merchant-123", request, "idem-key-2");

        Assert.Equal(0, _bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_InvalidRequest_ReturnsValidationErrors()
    {
        var request = BuildValidRequest() with { Cvv = "ab", Amount = 0 };

        var result = await _sut.ProcessPaymentAsync("merchant-123", request, "idem-key-3");

        Assert.Equal(PaymentStatus.Rejected, result.Status);
        Assert.NotEmpty(result.ValidationErrors);
    }

    // -------------------------------------------------------------------------
    // ProcessPayment — bank responses
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_BankAuthorizes_ReturnsAuthorized()
    {
        _bankClient.NextResponse = new BankSimulatorResponse
        {
            Authorized = true,
            AuthorizationCode = "AUTH-001"
        };

        var result = await _sut.ProcessPaymentAsync(
            "merchant-123", BuildValidRequest(), "idem-key-4");

        Assert.Equal(PaymentStatus.Authorized, result.Status);
    }

    [Fact]
    public async Task ProcessPayment_BankDeclines_ReturnsDeclined()
    {
        _bankClient.NextResponse = new BankSimulatorResponse
        {
            Authorized = false,
            AuthorizationCode = null
        };

        var result = await _sut.ProcessPaymentAsync(
            "merchant-123", BuildValidRequest(), "idem-key-5");

        Assert.Equal(PaymentStatus.Declined, result.Status);
    }

    [Fact]
    public async Task ProcessPayment_Authorized_PersistsPaymentWithCorrectStatus()
    {
        _bankClient.NextResponse = new BankSimulatorResponse
        {
            Authorized = true,
            AuthorizationCode = "AUTH-002"
        };

        var result = await _sut.ProcessPaymentAsync(
            "merchant-123", BuildValidRequest(), "idem-key-6");

        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatus.Authorized, stored.Status);
    }

    [Fact]
    public async Task ProcessPayment_StoresLastFourDigitsOnly()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        var request = BuildValidRequest() with { CardNumber = "2222405343248877" };
        var result = await _sut.ProcessPaymentAsync(
            "merchant-123", request, "idem-key-7");

        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.Equal("8877", stored!.CardNumberLastFour);
    }

    [Fact]
    public async Task ProcessPayment_NeverStoresFullCardNumber()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        var request = BuildValidRequest() with { CardNumber = "2222405343248877" };
        await _sut.ProcessPaymentAsync("merchant-123", request, "idem-key-8");

        // No stored entity should contain the full card number
        Assert.DoesNotContain(
            _repository.AllPayments,
            p => p.CardNumberLastFour == "2222405343248877");
    }

    // -------------------------------------------------------------------------
    // Idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_SameIdempotencyKey_ReturnsSameResult()
    {
        _bankClient.NextResponse = new BankSimulatorResponse
        {
            Authorized = true,
            AuthorizationCode = "AUTH-003"
        };

        var result1 = await _sut.ProcessPaymentAsync(
            "merchant-123", BuildValidRequest(), "idem-key-9");

        // Second call with same key — bank should NOT be called again
        var result2 = await _sut.ProcessPaymentAsync(
            "merchant-123", BuildValidRequest(), "idem-key-9");

        Assert.Equal(result1.PaymentId, result2.PaymentId);
        Assert.Equal(1, _bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_DifferentIdempotencyKeys_CreatesTwoPayments()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        var result1 = await _sut.ProcessPaymentAsync(
            "merchant-123", BuildValidRequest(), "idem-key-10");
        var result2 = await _sut.ProcessPaymentAsync(
            "merchant-123", BuildValidRequest(), "idem-key-11");

        Assert.NotEqual(result1.PaymentId, result2.PaymentId);
        Assert.Equal(2, _bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_SameIdempotencyKeyDifferentMerchant_CreatesTwoPayments()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        var result1 = await _sut.ProcessPaymentAsync(
            "merchant-a", BuildValidRequest(), "same-key");
        var result2 = await _sut.ProcessPaymentAsync(
            "merchant-b", BuildValidRequest(), "same-key");

        Assert.NotEqual(result1.PaymentId, result2.PaymentId);
    }

    // -------------------------------------------------------------------------
    // GetPayment
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPayment_ExistingId_ReturnsPayment()
    {
        var entity = BuildEntity("merchant-123");
        _repository.Add(entity);

        var result = await _sut.GetPaymentAsync("merchant-123", entity.Id);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result.Id);
    }

    [Fact]
    public async Task GetPayment_UnknownId_ReturnsNull()
    {
        var result = await _sut.GetPaymentAsync("merchant-123", Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPayment_WrongMerchant_ReturnsNull()
    {
        var entity = BuildEntity("merchant-a");
        _repository.Add(entity);

        var result = await _sut.GetPaymentAsync("merchant-b", entity.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPayment_SecondCall_ReturnsCachedResult()
    {
        var entity = BuildEntity("merchant-123");
        _repository.Add(entity);

        await _sut.GetPaymentAsync("merchant-123", entity.Id);

        // Remove from repository — service should still return from cache
        _repository.Remove(entity.Id);

        var result = await _sut.GetPaymentAsync("merchant-123", entity.Id);

        Assert.NotNull(result);
    }

    // -------------------------------------------------------------------------
    // Builders + fakes
    // -------------------------------------------------------------------------

    private static PostPaymentRequest BuildValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static PaymentEntity BuildEntity(string merchantId) => new()
    {
        Id = Guid.NewGuid(),
        MerchantId = merchantId,
        Status = PaymentStatus.Authorized,
        CardNumberLastFour = "8877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        RowVersion = 1
    };
}

// ---------------------------------------------------------------------------
// Fakes — in-process test doubles, no Moq needed
// ---------------------------------------------------------------------------

public sealed class FakePaymentsRepository : IPaymentsRepository
{
    private readonly List<PaymentEntity> _store = new();

    public IReadOnlyList<PaymentEntity> AllPayments => _store.AsReadOnly();

    public void Add(PaymentEntity entity) => _store.Add(entity);

    public PaymentEntity? Get(Guid id, string merchantId) =>
        _store.FirstOrDefault(p => p.Id == id && p.MerchantId == merchantId);

    public PaymentEntity? GetByIdempotencyKey(string key, string merchantId) =>
        _store.FirstOrDefault(p =>
            p.IdempotencyKey == key && p.MerchantId == merchantId);

    public bool Update(PaymentEntity entity, int expectedRowVersion)
    {
        var stored = _store.FirstOrDefault(p => p.Id == entity.Id);
        if (stored is null || stored.RowVersion != expectedRowVersion)
            return false;

        _store.Remove(stored);
        entity.RowVersion = expectedRowVersion + 1;
        _store.Add(entity);
        return true;
    }

    public void Remove(Guid id) =>
        _store.RemoveAll(p => p.Id == id);
}

public sealed class FakeBankSimulatorClient : IBankSimulatorClient
{
    public int CallCount { get; private set; }
    public BankSimulatorResponse NextResponse { get; set; } = new() { Authorized = true };

    public Task<BankSimulatorResponse?> ProcessPaymentAsync(
        BankSimulatorRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult<BankSimulatorResponse?>(NextResponse);
    }
}