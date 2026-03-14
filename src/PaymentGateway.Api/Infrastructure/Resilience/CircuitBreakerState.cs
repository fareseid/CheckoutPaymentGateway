using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentGateway.Api.Infrastructure.Resilience
{
    /// <summary>
    /// Represents the three states of a circuit breaker.
    /// </summary>
    public enum CircuitBreakerState
    {
        Closed,    // normal — requests flow through
        Open,      // tripped — requests fail immediately
        HalfOpen   // recovery probe — one request allowed through
    }
}
