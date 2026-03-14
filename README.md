# Payment Gateway — Design Decisions & Assumptions

## Architecture overview

Single ASP.NET Core Web API project. Separation of concerns enforced by
folder structure rather than multiple projects — the exercise scope does
not justify the overhead of a multi-project solution.
```
Api/
  Controllers/V1/   — HTTP concerns only, no business logic
  Domain/           — Entities, enums — pure, no infrastructure dependencies  
  Infrastructure/   — Repository, bank HTTP client, circuit breaker
  Middleware/       — Cross-cutting pipeline concerns
  Models/           — API request/response DTOs
  Services/         — Orchestration and business logic
  Validation/       — Request validation rules
  Configuration/    — Strongly-typed options
```

---

## Key design decisions

### 1. Two payment status enums

`PaymentRecordStatus` (domain) tracks the full internal lifecycle:
`Processing → Authorized | Declined | Failed`

`PaymentStatus` (API) is the merchant-facing contract:
`Authorized | Declined | Rejected`

`Processing` and `Failed` are internal states — merchants never see them.
`Failed` maps to `Rejected` via `ToApiStatus()` extension method.
This keeps the API contract stable even if internal states evolve.

### 2. Persist before calling the bank

The payment record is written as `Processing` before the bank HTTP call.
This guarantees:
- The merchant always receives a payment ID, even if the bank call fails
- A `Failed` record exists for ops visibility and debugging
- Idempotency works correctly on retry — the key is already in the store

### 3. No retry on bank calls

Payment requests are not idempotent at the bank level. If the bank
processed the charge but the network dropped the response, a retry
would double-charge the customer. The correct pattern is:
```
Bank fails → persist as Failed → merchant resubmits with new request
```

Our idempotency key prevents duplicate processing on our side.

### 4. Idempotency via cache + repository

Idempotency is checked at two layers:
1. `IMemoryCache` — fast in-process lookup, avoids repository hit on warm path
2. `IPaymentsRepository` — authoritative fallback when cache is cold

Cache key: `payment:{merchantId}:{idempotencyKey}`
Scoped per merchant — merchant A's key never collides with merchant B's.

If a `Processing` record is found on idempotency check, it maps to
`Rejected` — the merchant should resubmit with a new key.

### 5. HMAC-SHA256 request signing

All mutating requests (POST, PUT, PATCH) require an `X-HMAC-Signature`
header containing the HMAC-SHA256 hex digest of the raw request body,
signed with a shared secret.

Signature comparison uses `CryptographicOperations.FixedTimeEquals`
to prevent timing attacks — a normal string comparison exits on the
first differing byte, leaking information about the expected value.

GET/HEAD/OPTIONS are exempt — no body to sign.

### 6. JWT authentication + merchant scoping

Merchants authenticate via JWT bearer tokens. The `merchant_id` claim
is extracted from `HttpContext.User` by the controller — no second
validation call needed since `UseAuthentication()` handles this.

Every repository query includes `merchantId` — a merchant can only
see their own payments. A valid payment ID belonging to another merchant
returns `404` (not `403`) to avoid confirming the payment exists.

### 7. Circuit breaker — no external libraries

Manual implementation with three states: `Closed → Open → HalfOpen`.
Opens after N consecutive failures, recovers after a configurable period.
HalfOpen allows exactly one probe request — success closes the circuit,
failure re-opens it.

All configuration is in `appsettings.json` under `BankSimulator` section.

### 8. Optimistic concurrency

In real life scenario, we'll rely on the database row version and database constraints for the write to handle concurrency while relying on the read-committed isolation for the read.
The application never increments `RowVersion` — that is the database's job.
A `false` return from `Update` means a concurrent write won — the service logs a warning.

### 9. Middleware pipeline order
```
GlobalExceptionMiddleware   — outermost, catches everything
CorrelationIdMiddleware     — sets TraceIdentifier early for all logs
RequestLoggingMiddleware    — logs with correct correlation ID
UseHttpsRedirection         — redirects HTTP to HTTPS
UseAuthentication           — establishes HttpContext.User from JWT
UseAuthorization            — enforces [Authorize] attributes  
HmacValidationMiddleware    — runs after auth, merchant identity known
```

Order is load-bearing — swapping any two adjacent middleware has
observable side effects.

### 10. Sensitive data never logged

Card numbers, CVV, and Authorization headers are never written to logs.
`RequestLoggingMiddleware` only logs method, path, status code, and duration.
The full card number exists only in memory during request processing —
only the last four digits are persisted.

---

## Assumptions

**Currency whitelist**: GBP, USD, EUR only. The exercise says "no more
than 3 currency codes" — these three cover the most common use cases.

**In-memory storage**: `PaymentsRepository` uses `List<PaymentEntity>`.
State is lost on restart. A real implementation would use a database
with a unique index on `(MerchantId, IdempotencyKey)`.

**Single instance**: The in-memory repository and circuit breaker are
not distributed. In a multi-instance deployment both would need to
move to shared infrastructure (Redis, distributed circuit breaker).

**JWT token issuance**: `JwtTokenService.GenerateToken` exists for
testing — in production a dedicated auth service would issue tokens.
No `/auth/token` endpoint is implemented.

**Bank simulator contract**: The simulator accepts `card_number`,
`expiry_date` (MM/YYYY), `currency`, `amount`, `cvv` and returns
`authorized` + `authorization_code`. This contract is treated as fixed.

**HTTPS in development**: `UseHttpsRedirection` is registered but
the bank simulator runs over plain HTTP. The redirect applies to
incoming merchant requests only, not outbound bank calls.

**No pagination**: `GET /api/v1/payments/{id}` retrieves a single
payment by ID. A list endpoint is out of scope for this exercise.

---