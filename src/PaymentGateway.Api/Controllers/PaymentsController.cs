using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;  
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers.V1;
 
public sealed class PaymentsController : ApiControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IValidator<PostPaymentRequest> _validator;


    public PaymentsController(
        IPaymentService paymentService,
        ILogger<PaymentsController> logger,
        IValidator<PostPaymentRequest> validator)
    {
        _paymentService = paymentService;
        _logger = logger;
        _validator = validator;
    }

    /// <summary>
    /// Retrieves a previously processed payment by ID.
    /// Only returns payments belonging to the authenticated merchant.
    /// </summary>
    [HttpGet("{id:guid}")] 
    public async Task<ActionResult<GetPaymentResponse>> GetPaymentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {


        var merchantId = User.FindFirstValue("merchant_id");
        if (merchantId is null)
            return Unauthorized("merchant_id claim missing.");
        _logger.LogInformation(
            "GetPayment. MerchantId: {merchantId} PaymentId: {PaymentId}",
            MerchantId, id);

        var paymentResult = await _paymentService.GetPaymentAsync(
            MerchantId, id, cancellationToken);

        if (paymentResult is null)
            return NotFound();

        return Ok(paymentResult);
    }

    /// <summary>
    /// Processes a new payment through the acquiring bank.
    /// Requires X-HMAC-Signature header for request integrity validation.
    /// Supports idempotent submission via Idempotency-Key header.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

        _logger.LogInformation(
            "PostPayment. MerchantId: {MerchantId} IdempotencyKey: {IdempotencyKey}",
            MerchantId, idempotencyKey ?? "none");

        if (idempotencyKey is null)
        {
            return BadRequest(new ApiErrorResponse
            {
                Code = "validation_error",
                Errors = new Dictionary<string, string[]> { { "Idempotency-Key", new[] { "Idempotency Key is missing." } }, },
                Message = "One or more validation errors occurred.",
                Timestamp = DateTimeOffset.UtcNow,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var validationResult = _validator.Validate(request);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Payment request rejected due to validation errors. " +
                "MerchantId: {MerchantId} Errors: {Errors}",
                MerchantId,
                string.Join(", ", validationResult.Errors.Select(e => $"{e.Field}: {e.Message}")));
            return BadRequest(new ApiErrorResponse { 
                Code = "validation_error",
                Errors = validationResult.Errors
                    .GroupBy(e => e.Field)
                    .ToDictionary(
                        g => g.Key, 
                        g => g.Select(e => e.Message).ToArray()),
                Message = "One or more validation errors occurred.",
                Timestamp = DateTimeOffset.UtcNow,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var result = await _paymentService.ProcessPaymentAsync(
            MerchantId, request, idempotencyKey, cancellationToken);

        return Ok(new PostPaymentResponse
        {
            Id = result.PaymentId,
            Status = result.Status.ToString(),
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,  
            Currency = request.Currency,
            Amount = request.Amount
        });
    }
}