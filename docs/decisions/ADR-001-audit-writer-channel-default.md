# ADR-001: Channel-Backed Audit Writer as Default

**Status:** Accepted
**Date:** 2026-03-19
**Component:** ModernMediator.Audit / AuditBehavior

---

## Context

The `AuditBehavior` must write an `AuditRecord` after every audited request completes. Two implementation strategies were considered for how that write is dispatched.

The first strategy is fire-and-forget: call `IAuditWriter.WriteAsync` without awaiting the result, swallowing any exceptions to prevent audit failures from propagating into the request pipeline. This is simple to implement and imposes no additional infrastructure.

The second strategy is a channel-backed queue: the behavior enqueues an `AuditRecord` into a `Channel<AuditRecord>` and returns immediately. A hosted background service drains the channel and calls `IAuditWriter.WriteAsync` on a dedicated thread, outside the request pipeline entirely.

## Decision

The channel-backed approach is the default implementation. Fire-and-forget is available as an opt-in via `AuditOptions`.

## Rationale

Fire-and-forget has a critical failure mode: if the application process crashes or is restarted between the moment the behavior fires the write and the moment the writer completes, the audit record is lost. For general-purpose web applications this may be acceptable. For safety-critical or regulated environments, a missing audit record is a compliance event, not just a data gap.

The channel approach decouples the write from the request pipeline without losing the record on application shutdown. The .NET hosted service infrastructure allows the background drainer to complete in-flight writes during graceful shutdown before the process exits, giving the audit record a materially better chance of surviving a restart or deployment.

The performance difference between the two approaches is negligible in practice. The channel enqueue is a non-blocking in-memory operation and adds no measurable latency to request dispatch.

Callers who genuinely prefer fire-and-forget because their audit writer is a structured logger with no persistence concern can opt in explicitly. Making the safer behavior the default means callers in regulated industries are protected without having to know to ask for it.

## Consequences

- The `ModernMediator` package takes a dependency on the .NET hosted services infrastructure for the background drainer, which is already a standard dependency in any ASP.NET Core application.
- Callers must register the background drainer via `AddAudit()` or it will not run. This is handled automatically by the extension method and does not require manual registration.
- Records in the channel are in-memory and will be lost if the process is killed without a graceful shutdown opportunity. This is documented as a known limitation. Callers requiring guaranteed delivery should implement a durable `IAuditWriter` backed by a database or message broker.
