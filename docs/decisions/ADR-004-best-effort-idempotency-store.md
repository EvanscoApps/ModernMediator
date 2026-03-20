# ADR-004: Best-Effort Idempotency Store with Durable Option via Separate Package

**Status:** Accepted
**Date:** 2026-03-19
**Component:** ModernMediator / IdempotencyBehavior, ModernMediator.Idempotency.EntityFramework

---

## Context

`IdempotencyBehavior` checks a fingerprint store before dispatching a request. If the fingerprint has been seen within the configured window, the cached result is returned without executing the handler. Two positions on what guarantee the store should provide were considered.

The first position is best-effort: the core package ships cache-backed store implementations (`InMemoryIdempotencyStore` and `DistributedIdempotencyStore`). Both can lose entries under cache pressure or misconfiguration, meaning a duplicate request could re-execute if its fingerprint has been evicted before the window expires. Callers are expected to make their handlers idempotent at the domain level as a backstop.

The second position is durable: the store must be backed by a database with a unique constraint on the fingerprint column, providing exactly-once execution guarantees that do not depend on cache retention.

## Decision

The core package ships best-effort store implementations and documents their limitations clearly. A separate package, `ModernMediator.Idempotency.EntityFramework`, provides a durable store backed by a relational database. Callers choose the implementation that matches their requirements.

## Rationale

The majority of use cases for idempotency in a mediator pipeline do not require exactly-once execution guarantees at the infrastructure level. Preventing duplicate webhook processing, avoiding redundant cache population, and short-circuiting repeated form submissions are all well-served by a distributed cache store. These callers should not pay for a database dependency they do not need.

Callers who do require exactly-once guarantees, such as financial transaction processors or safety-critical ingestion pipelines, need a store that cannot lose entries. A unique constraint on the fingerprint column in a relational database is the correct mechanism: if two concurrent requests race with the same fingerprint, the database rejects the second insert at the constraint level rather than relying on application-layer locking.

Shipping both as separate packages respects the principle that callers should not acquire infrastructure dependencies they have not asked for. A caller using the in-memory store for development and the distributed cache store for production is not affected by the existence of the EF package. A caller who needs durable exactly-once semantics installs the EF package and registers the durable store via the standard `AddIdempotency()` extension method.

This pattern is consistent with how `ModernMediator.Audit.EntityFramework` is structured and establishes a reusable convention for optional persistence packages across the ecosystem.

## Consequences

- `InMemoryIdempotencyStore` and `DistributedIdempotencyStore` are documented as best-effort with explicit acknowledgment that cache eviction can result in handler re-execution within a configured window. Callers are advised to design handlers to be idempotent at the domain level when using these stores.
- `ModernMediator.Idempotency.EntityFramework` ships a `EfCoreIdempotencyStore` backed by a dedicated `IdempotencyDbContext` following the same dedicated context pattern established in ADR-003.
- The fingerprint column in the durable store carries a unique constraint enforced at the database level. Duplicate inserts within the window return the stored result without executing the handler, regardless of application-layer race conditions.
- Callers who need durable idempotency but do not use EF Core can implement `IIdempotencyStore` against any persistence technology. The interface is intentionally minimal to make this straightforward.
