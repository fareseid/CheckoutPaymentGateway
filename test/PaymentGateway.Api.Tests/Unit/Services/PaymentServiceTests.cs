using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Domain.Entities;
using PaymentGateway.Api.Domain.Enums;
using PaymentGateway.Api.Infrastructure.BankSimulator;
using PaymentGateway.Api.Infrastructure.Repositories;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services; 

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
            _cache,
            NullLogger<PaymentService>.Instance);
    } 

    // -------------------------------------------------------------------------
    // Processing — record persisted before bank call
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_ValidRequest_PersistedAsProcessingBeforeBankCall()
    {
        PaymentRecordStatus? statusDuringBankCall = null;
        _bankClient.OnProcessPayment = () =>
        {
            statusDuringBankCall = _repository.AllPayments.FirstOrDefault()?.Status;
        };

        await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-5");

        Assert.Equal(PaymentRecordStatus.Processing, statusDuringBankCall);
    }

    [Fact]
    public async Task ProcessPayment_ValidRequest_RecordExistsEvenIfBankCallThrows()
    {
        _bankClient.ShouldThrow = true;

        var result = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-6");

        Assert.NotEqual(Guid.Empty, result.PaymentId);
        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.NotNull(stored);
    }

    // -------------------------------------------------------------------------
    // Bank responses — Authorized / Declined
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_BankAuthorizes_PersistsAsAuthorized()
    {
        _bankClient.NextResponse = new BankSimulatorResponse
        {
            Authorized = true,
            AuthorizationCode = "AUTH-002"
        };

        var result = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-8");

        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.Equal(PaymentRecordStatus.Authorized, stored!.Status);
    }

    [Fact]
    public async Task ProcessPayment_BankDeclines_PersistsAsDeclined()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = false };

        var result = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-11");

        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.Equal(PaymentRecordStatus.Declined, stored!.Status);
    }

    // -------------------------------------------------------------------------
    // Bank failures — Failed internally, Rejected on API surface
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_BankThrows_PersistsAsFailed()
    {
        _bankClient.ShouldThrow = true;

        var result = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-13");

        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.Equal(PaymentRecordStatus.Failed, stored!.Status);
    }
      

    [Fact]
    public async Task ProcessPayment_BankServiceUnavailable_PersistsAsFailed()
    {
        _bankClient.ShouldThrowServiceUnavailable = true;

        var result = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-16");

        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.Equal(PaymentRecordStatus.Failed, stored!.Status);
    }

    // -------------------------------------------------------------------------
    // Card number masking
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_StoresLastFourDigitsOnly()
    {
        var request = BuildValidRequest() with { CardNumber = "2222405343248877" };

        var result = await _sut.ProcessPaymentAsync("merchant-123", request, "idem-17");

        var stored = _repository.Get(result.PaymentId, "merchant-123");
        Assert.Equal("8877", stored!.CardNumberLastFour);
    }


    // -------------------------------------------------------------------------
    // Idempotency — cache layer
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_SameIdempotencyKey_CacheHit_BankCalledOnlyOnce()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-19");
        await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-19");

        Assert.Equal(1, _bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_SameIdempotencyKey_CacheHit_ReturnsSamePaymentId()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        var result1 = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-20");
        var result2 = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-20");

        Assert.Equal(result1.PaymentId, result2.PaymentId);
    }

    [Fact]
    public async Task ProcessPayment_SameIdempotencyKey_RepositoryHit_ReturnsSamePaymentId()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        // First call — populates repository
        var result1 = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-21");

        // Evict from cache to force repository lookup on second call
        _cache.Remove($"payment:merchant-123:idem-21");

        var result2 = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-21");

        Assert.Equal(result1.PaymentId, result2.PaymentId);
        Assert.Equal(1, _bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_DifferentIdempotencyKeys_CreatesTwoPayments()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        var result1 = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-22");
        var result2 = await _sut.ProcessPaymentAsync("merchant-123", BuildValidRequest(), "idem-23");

        Assert.NotEqual(result1.PaymentId, result2.PaymentId);
        Assert.Equal(2, _bankClient.CallCount);
    }

    [Fact]
    public async Task ProcessPayment_SameKeyDifferentMerchant_CreatesTwoPayments()
    {
        _bankClient.NextResponse = new BankSimulatorResponse { Authorized = true };

        var result1 = await _sut.ProcessPaymentAsync("merchant-a", BuildValidRequest(), "same-key");
        var result2 = await _sut.ProcessPaymentAsync("merchant-b", BuildValidRequest(), "same-key");

        Assert.NotEqual(result1.PaymentId, result2.PaymentId);
        Assert.Equal(2, _bankClient.CallCount);
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

        _repository.Remove(entity.Id);

        var result = await _sut.GetPaymentAsync("merchant-123", entity.Id);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result.Id);
    }

    // -------------------------------------------------------------------------
    // Builders
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
        Status = PaymentRecordStatus.Authorized,
        CardNumberLastFour = "8877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100
    };
}

// -----------------------------------------------------------------------------
// Fakes
// -----------------------------------------------------------------------------

public sealed class FakePaymentsRepository : IPaymentsRepository
{
    private readonly List<PaymentEntity> _store = new();

    public IReadOnlyList<PaymentEntity> AllPayments => _store.AsReadOnly();

    public void Add(PaymentEntity entity) => _store.Add(entity);

    public PaymentEntity? Get(Guid id, string merchantId) =>
        _store.FirstOrDefault(p => p.Id == id && p.MerchantId == merchantId);

    public PaymentEntity? GetByIdempotencyKey(string key, string merchantId) =>
        _store.FirstOrDefault(p =>
            p.IdempotencyKey == key &&
            p.MerchantId == merchantId);

    public bool Update(PaymentEntity entity)
    {
        var index = _store.FindIndex(p => p.Id == entity.Id);
        if (index == -1) return false;
        _store[index] = entity;
        return true;
    }

    public void Remove(Guid id) =>
        _store.RemoveAll(p => p.Id == id);
}

public sealed class FakeBankSimulatorClient : IBankSimulatorClient
{
    public int CallCount { get; private set; }
    public bool ShouldThrow { get; set; }
    public bool ShouldThrowServiceUnavailable { get; set; }
    public BankSimulatorResponse NextResponse { get; set; } = new() { Authorized = true };
    public Action? OnProcessPayment { get; set; }

    public Task<BankSimulatorResponse?> ProcessPaymentAsync(
        BankSimulatorRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        OnProcessPayment?.Invoke();

        if (ShouldThrowServiceUnavailable)
            throw new HttpRequestException(
                "Service Unavailable",
                inner: null,
                statusCode: System.Net.HttpStatusCode.ServiceUnavailable);

        if (ShouldThrow)
            throw new HttpRequestException("Bank simulator unavailable.");

        return Task.FromResult<BankSimulatorResponse?>(NextResponse);
    }
}