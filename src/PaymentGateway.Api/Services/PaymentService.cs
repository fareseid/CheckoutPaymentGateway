using System.Net;

using Microsoft.Extensions.Caching.Memory;

using PaymentGateway.Api.Domain.Entities; 
using PaymentGateway.Api.Domain.Enums;
using PaymentGateway.Api.Infrastructure.BankSimulator;
using PaymentGateway.Api.Infrastructure.Repositories;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentsRepository _repository;
    private readonly IBankSimulatorClient _bankClient; 
    private readonly IMemoryCache _cache;
    private readonly ILogger<PaymentService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public PaymentService(
        IPaymentsRepository repository,
        IBankSimulatorClient bankClient, 
        IMemoryCache cache,
        ILogger<PaymentService> logger)
    {
        _repository = repository;
        _bankClient = bankClient; 
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> ProcessPaymentAsync(
        string merchantId,
        PostPaymentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {

        var cacheKey = $"payment:{merchantId}:{idempotencyKey}";
        // ------------------------------------------------------------------
        // Step 1 — Idempotency check
        // ------------------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {


            if (_cache.TryGetValue(cacheKey, out PaymentEntity? existing))
            {
                _logger.LogDebug(
                    "Payment cache hit. IdempotencyKey: {idempotencyKey} MerchantId:{merchantId}",
                    idempotencyKey,merchantId);
            }
            else
            {
                existing = _repository.GetByIdempotencyKey(idempotencyKey, merchantId); 
            }

            if (existing is not null)
                _cache.Set(cacheKey, existing, CacheDuration);

            if (existing is not null)
            {
                // Edge case: a previous request persisted the record but the bank call
                // is still in flight (or failed to update). We return Processing mapped
                // to Rejected — the merchant should retry with a new idempotency key
                // or query the payment later to see the final status.
                _logger.LogInformation(
                    "Idempotent request detected. MerchantId: {MerchantId} " +
                    "IdempotencyKey: {IdempotencyKey} PaymentId: {PaymentId}",
                    merchantId, idempotencyKey, existing.Id);


                // Map Domain payment status to API payment status for the result
                return new ProcessPaymentResult
                {
                    PaymentId = existing.Id,
                    Status = existing.Status.ToApiStatus()
                };
            }
        }

        // ------------------------------------------------------------------
        // Step 2 — Persist as Processing BEFORE calling the bank
        // This guarantees we always have a record, even if the bank call
        // fails. The merchant receives a payment ID they can query later.
        // ------------------------------------------------------------------
        var entity = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            IdempotencyKey = idempotencyKey,
            Status = PaymentRecordStatus.Processing,
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        _repository.Add(entity);

        _logger.LogInformation(
            "Payment created as Processing. MerchantId: {MerchantId} PaymentId: {PaymentId}",
            merchantId, entity.Id);

        // ------------------------------------------------------------------
        // Step 3 — Call acquiring bank
        // ------------------------------------------------------------------
        BankSimulatorResponse? bankResponse = null;
        try
        {
            var bankRequest = new BankSimulatorRequest
            {
                CardNumber = request.CardNumber,
                ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
                Currency = request.Currency,
                Amount = request.Amount,
                Cvv = request.Cvv
            };

            bankResponse = await _bankClient.ProcessPaymentAsync(
                bankRequest, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            // Service Unavailable — likely the bank simulator is down or overloaded. This is a known failure mode, so we catch it separately to log it clearly.
            _logger.LogError(ex,
                "Bank Service Unavailable. MerchantId: {MerchantId} PaymentId: {PaymentId}",
                merchantId, entity.Id);

            entity.Status = PaymentRecordStatus.Failed;
            entity.LastUpdatedAt = DateTimeOffset.UtcNow;
            _repository.Update(entity);

            return new ProcessPaymentResult
            {
                PaymentId = entity.Id,
                Status = entity.Status.ToApiStatus()
            };
        }
        catch (Exception ex)
        {
            // Bank call failed — timeout, circuit open, or unhandled error.
            // Update the persisted record to Failed so the merchant and ops
            // team know what happened. Never leave it stuck in Processing.
            _logger.LogError(ex,
                "Bank call failed. MerchantId: {MerchantId} PaymentId: {PaymentId}",
                merchantId, entity.Id);

            entity.Status = PaymentRecordStatus.Failed;
            entity.LastUpdatedAt = DateTimeOffset.UtcNow;
            _repository.Update(entity);

            return new ProcessPaymentResult
            {
                PaymentId = entity.Id,
                Status = entity.Status.ToApiStatus()
            };
        }

        // ------------------------------------------------------------------
        // Step 4 — Update persisted record with bank outcome
        // ------------------------------------------------------------------
        entity.Status = bankResponse is { Authorized: true }
            ? PaymentRecordStatus.Authorized
            : PaymentRecordStatus.Declined;

        entity.AuthorizationCode = bankResponse?.AuthorizationCode;

        var updated = _repository.Update(entity);
        if (!updated)
        {
            // This would mean a concurrent request updated the same payment
            // between our Add and Update — should not happen in practice
            // since the payment ID is freshly minted, but we log it.
            _logger.LogWarning(
                "Optimistic concurrency conflict on payment update. " +
                "MerchantId: {MerchantId} PaymentId: {PaymentId}",
                merchantId, entity.Id);
        }
        else
        {
            _cache.Set(cacheKey, entity, CacheDuration);
        }

        _logger.LogInformation(
            "Payment finalised. MerchantId: {MerchantId} PaymentId: {PaymentId} " +
            "Status: {Status}",
            merchantId, entity.Id, entity.Status);

        return new ProcessPaymentResult
        {
            PaymentId = entity.Id,
            Status = entity.Status.ToApiStatus()
        };
    }

    public async Task<GetPaymentResult?> GetPaymentAsync(
        string merchantId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"payment:{merchantId}:{paymentId}";

        if (_cache.TryGetValue(cacheKey, out PaymentEntity? cached))
        {
            _logger.LogDebug(
                "Payment cache hit. MerchantId: {MerchantId} PaymentId: {PaymentId}",
                merchantId, paymentId);
            return new GetPaymentResult
            {
                Id = cached?.Id?? Guid.Empty,
                Status = cached?.Status.ToApiStatus() ?? PaymentStatus.Rejected,
                CardNumberLastFour = cached?.CardNumberLastFour ?? string.Empty,
                ExpiryMonth = cached?.ExpiryMonth??0,
                ExpiryYear = cached?.ExpiryYear??0,
                Currency = cached?.Currency??string.Empty,
                Amount = cached?.Amount??0
            };
        }

        var entity = _repository.Get(paymentId, merchantId);

        if(entity is null)
        {
            return null;
        }
        else
        {
            _cache.Set(cacheKey, entity, CacheDuration);
        } 
           

        return await Task.FromResult(new GetPaymentResult
        {
                Id = entity?.Id ?? Guid.Empty,
                Status = entity?.Status.ToApiStatus() ?? PaymentStatus.Rejected,
                CardNumberLastFour = entity?.CardNumberLastFour ?? string.Empty,
                ExpiryMonth = entity?.ExpiryMonth ?? 0,
                ExpiryYear = entity?.ExpiryYear ?? 0,
                Currency = entity?.Currency ?? string.Empty,
                Amount = entity?.Amount ?? 0
        });
    }
} 