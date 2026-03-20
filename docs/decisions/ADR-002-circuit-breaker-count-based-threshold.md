# ADR-002: Count-Based Circuit Breaker Threshold

**Status:** Accepted
**Date:** 2026-03-19
**Component:** ModernMediator / CircuitBreakerBehavior

---

## Context

Polly v8's `CircuitBreakerStrategyOptions` uses a ratio-based failure model. The circuit opens when the failure rate exceeds `FailureRatio` (a value between 0.0 and 1.0) within a sampling window, subject to a `MinimumThroughput` floor that prevents the circuit from opening on a single failure during low traffic.

The `[CircuitBreaker]` attribute must expose a `failureThreshold` parameter. Two interpretations of that parameter were considered.

The first interpretation is ratio-based: `failureThreshold` is a percentage (e.g., 50 means 50% failure rate). This maps directly to Polly's `FailureRatio` and exposes the full nuance of the model.

The second interpretation is count-based: `failureThreshold` is an absolute number of failures (e.g., 5 means trip after 5 failures within the sampling window). This is implemented by setting `FailureRatio = 1.0` and `MinimumThroughput = failureThreshold`, so the circuit opens only when every request in the window has failed and at least `failureThreshold` requests have been observed.

## Decision

The `failureThreshold` parameter is interpreted as an absolute failure count. The mapping to Polly's `FailureRatio = 1.0` and `MinimumThroughput = failureThreshold` is handled internally by the behavior and is not exposed to the caller.

## Rationale

Developers reasoning about circuit breaker configuration in an attribute think in concrete failure counts, not ratios. "Trip after 5 failures" is immediately understandable at the call site. "Trip at a 0.5 failure ratio with a minimum throughput of 5" requires the developer to understand both parameters and how they interact before they can reason about what the circuit will actually do in production.

The count-based model also aligns with how circuit breakers are typically described in architecture documentation and incident reviews. A statement like "the circuit is configured to open after 5 consecutive failures within a 10-second window" is directly traceable to the attribute declaration `[CircuitBreaker(failureThreshold: 5, durationSeconds: 30)]` without any mental translation.

Callers who need ratio-based control can implement a custom Polly pipeline and register it via `ICircuitBreakerRegistry` directly. The attribute is not the only configuration surface.

## Consequences

- The count-based model is slightly more conservative than a ratio model at low traffic volumes. A single burst of exactly `failureThreshold` failures will trip the circuit even if earlier requests in the window succeeded. This is the correct behavior for protecting external dependencies where any concentration of failures is a signal worth acting on.
- The internal mapping to `FailureRatio = 1.0` and `MinimumThroughput = failureThreshold` is documented so that callers who inspect the underlying Polly pipeline are not surprised by the values they see.
- `SamplingDurationSeconds` and `MinimumThroughput` remain exposed on the attribute for callers who need to tune the sampling window. Setting `MinimumThroughput` explicitly on the attribute overrides the count-based default mapping.
