namespace PaymentGateway.Api.Infrastructure.Resilience;

/// <summary>
/// Manual circuit breaker implementation — no external libraries.
///
/// Closed  → counts failures → threshold hit → Open
/// Open    → rejects immediately → recovery period elapsed → HalfOpen
/// HalfOpen → one probe request →
///     success → Closed
///     failure → Open again
/// </summary>
public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly object _lock = new();

    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount;
    private DateTimeOffset _openedAt;

    public CircuitBreakerState State => _state;

    public CircuitBreaker(int failureThreshold, int openSeconds)
    {
        _failureThreshold = failureThreshold;
        _openDuration = TimeSpan.FromSeconds(openSeconds);
    }

    /// <summary>
    /// Returns true if a request is allowed through.
    /// Open circuit returns false immediately — no network call made.
    /// </summary>
    public bool IsRequestAllowed()
    {
        lock (_lock)
        {
            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    return true;

                case CircuitBreakerState.Open:
                    if (DateTimeOffset.UtcNow - _openedAt >= _openDuration)
                    {
                        // Recovery period elapsed — allow one probe
                        _state = CircuitBreakerState.HalfOpen;
                        return true;
                    }
                    return false;

                case CircuitBreakerState.HalfOpen:
                    // Only one probe at a time — subsequent requests still blocked
                    return false;

                default:
                    return false;
            }
        }
    }

    /// <summary>Call when a request succeeds.</summary>
    public void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }
    }

    /// <summary>Call when a request fails.</summary>
    public void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;

            if (_state == CircuitBreakerState.HalfOpen ||
                _failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
                _openedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}