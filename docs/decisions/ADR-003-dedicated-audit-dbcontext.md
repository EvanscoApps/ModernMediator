# ADR-003: Dedicated AuditDbContext in ModernMediator.Audit.EntityFramework

**Status:** Accepted
**Date:** 2026-03-19
**Component:** ModernMediator.Audit.EntityFramework / EfCoreAuditWriter

---

## Context

`ModernMediator.Audit.EntityFramework` provides an `EfCoreAuditWriter` implementation that persists `AuditRecord` instances to a relational database via Entity Framework Core. Two approaches to `DbContext` access were considered.

The first approach injects the caller's existing application `DbContext` directly into the writer. This allows audit records to share the same database connection and transaction as the business operation being audited.

The second approach uses a dedicated `AuditDbContext` registered with its own lifetime, separate from any `DbContext` the caller uses for business data.

## Decision

`EfCoreAuditWriter` uses a dedicated `AuditDbContext`. The caller's application `DbContext` is never injected into or accessed by the writer.

## Rationale

Injecting the caller's `DbContext` creates two categories of problem.

The first is concurrency. EF Core `DbContext` instances are not thread-safe. `AuditBehavior` writes audit records from a channel-backed background service running on a separate thread. If that background service shares a `DbContext` instance with the request pipeline, concurrent access is guaranteed under any non-trivial load. The resulting behavior is undefined and the failure mode is difficult to reproduce in testing.

The second is transactional coupling. An audit write must succeed or fail independently of the business operation. If the audit write is enlisted in the same transaction as the business operation and that transaction is rolled back, the audit record disappears. This defeats the purpose of auditing. Conversely, if the audit write fails and it is in the same transaction, it could roll back a business operation that succeeded. Neither outcome is acceptable.

A dedicated `AuditDbContext` eliminates both problems. It has its own connection, its own transaction scope, and its own lifetime. The audit write either succeeds or fails on its own terms without any coupling to business data operations.

## Consequences

- Callers must add `AuditDbContext` to their EF Core migrations or configure it against a separate audit database. Setup instructions are provided in the package README.
- The `AuditDbContext` schema is minimal: a single `AuditRecords` table with columns corresponding to the `AuditRecord` type. An `IEntityTypeConfiguration<AuditRecord>` is shipped with the package for callers to include in `OnModelCreating` if they prefer to host audit records in their existing database rather than a dedicated one.
- Callers who want audit records to be transactionally consistent with their business data should implement a custom `IAuditWriter` using their own `DbContext` and accept the concurrency responsibility explicitly. This is a deliberate choice that should not be the default experience.
