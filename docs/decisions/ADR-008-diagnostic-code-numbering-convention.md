# ADR-008: Diagnostic code numbering convention

**Status:** Accepted
**Date:** 2026-04-29
**Component:** ModernMediator, ModernMediator.Generators

---

## Context

ModernMediator emits diagnostics through two distinct channels. The first is the source generator, which inspects handler declarations at compile time and reports findings as Roslyn diagnostics that surface in the IDE error list and the build output. The second is runtime, where the dispatcher and supporting infrastructure throw exceptions when a request cannot be served. Through v2.2, every MM-prefix code in the codebase has been a source-generator diagnostic. Runtime exceptions have used prose messages without codes, so users searching for an MM-prefix string in their bug reports have always landed on a compile-time diagnostic.

The dispatcher overload mismatch fix in ADR-009 introduces the first runtime condition that warrants a code. Without a numbering convention, runtime codes and source-generator codes would intermingle in a single sequential range, and consumers reading a code would have to consult an external table to know which channel produced it. With the convention, the channel is visible from the code itself.

The existing codes occupy MM001 through MM008 for source-generator diagnostics with severity Error, Warning, or Info that fire on handler declaration sites, plus MM100 for `GeneratorSuccess`, an info-level diagnostic the generator emits once per compilation reporting how many handlers, behaviors, and processors were registered. The MM1xx range therefore already has a precedent, distinct from MM0xx, even though only one code currently occupies it.

## Decision

Three slot ranges are reserved for ModernMediator diagnostic codes:

- **MM0xx**: Source-generator and compile-time analyzer diagnostics that fire on handler, behavior, or processor declarations and surface in the IDE error list. Severity is typically Error or Warning. MM001 through MM008 currently occupy this range.
- **MM1xx**: Source-generator informational diagnostics that report on the generation process itself rather than on user code. Severity is typically Info. MM100 (`GeneratorSuccess`) is the precedent and currently the only occupant.
- **MM2xx**: Runtime diagnostic codes. Surfaced as bracketed prefixes (`[MM2xx]`) in exception messages, not as Roslyn diagnostics. The first occupant under this convention is MM201 (dispatcher overload mismatch, ADR-009). MM200 is reserved by the AspNetCore endpoint generator for a pre-existing compile-time diagnostic ("Invalid HTTP method on `[Endpoint]` attribute," introduced in v2.0); per the forward-only nature of this convention it keeps that identifier rather than being relocated. Runtime codes introduced under this convention therefore start at MM201 and increment from there. MM202 (generated-handler-not-resolved) is the second occupant: emitted by the source-generated `Send` and `CreateStream` extension methods in `MediatorGenerator.cs` when `IServiceProvider.GetService<IRequestHandler<,>>` (or `IStreamRequestHandler<,>`) returns null at dispatch time, surfaced as `InvalidOperationException` with a `[MM202]` prefix and a guiding message that points at `AddModernMediatorGenerated()` registration. Subsequent runtime codes increment from there (MM203, MM204, ...).

The slot is not encoded in any descriptor metadata; it is a contributor convention. New codes are placed in the lowest free slot of the appropriate range. When MM099 is reached, the next compile-time diagnostic moves to MM300 or higher rather than spilling into MM1xx.

## Rationale

Two alternatives were considered.

The first was to maintain a single sequential range across all channels, treating the MM-prefix as a flat namespace. This would have kept the existing codes' numbering intact (MM001 through MM100 are all sequential) and avoided introducing a convention. It was rejected because it provides no information at the call site of a user reporting a bug. A user pasting "MM015 fired in our build" reveals nothing about whether to look at the source generator or the runtime path; with the slot convention, the channel is immediately visible.

The second was to use a different prefix entirely for runtime codes (e.g., `MMR200` or `MM-RT-001`). This would have made the channel distinction even more visible. It was rejected for ergonomic reasons: a single MM-prefix means users grepping their codebase or bug reports use one search term, and tools that already recognize the MM-prefix (the IDE's code-action surface, for instance) continue to work without reconfiguration. The two-digit slot range is sufficient signal without adding a second prefix.

The chosen MM2xx slot for runtime codes leaves MM0xx and MM1xx undisturbed. Existing MM001 through MM008 keep their meaning, and the only currently-defined MM1xx code (MM100) keeps its position. The convention is forward-only: it constrains where new codes are placed, not where existing codes sit. The pre-existing MM200 emitted by the AspNetCore endpoint generator (Invalid HTTP method on `[Endpoint]`, v2.0) is grandfathered under this rule, so the first runtime code introduced under the convention takes MM201 rather than colliding with the established compile-time identifier.

The capacity per slot (100 codes each) is comfortable for the foreseeable lifetime of ModernMediator's diagnostic surface. If MM0xx exhausts faster than expected, the convention can be revisited; the present commitment is that contributors place new codes in the slot whose channel matches their diagnostic's emission path.

## Consequences

`DiagnosticDescriptors.cs` in `ModernMediator.Generators` is the source of truth for all MM-prefix codes, both compile-time (as `DiagnosticDescriptor` instances) and runtime (as `public const string` values). Runtime codes are constants because they do not flow through Roslyn's diagnostic pipeline; the constant exists so consumers grepping the codebase find a single declaration rather than searching for the bracketed string in exception throw sites.

Future runtime diagnostics follow the MM201 pattern: a const string in `DiagnosticDescriptors`, surfaced via a bracketed prefix in the exception message, with a corresponding ADR documenting the runtime condition the code represents.

Documentation that lists ModernMediator's diagnostic codes (README, API reference, generator output) groups codes by slot and labels the channel. Code consumers reading the documentation see the channel before the code's specific meaning.

The convention does not constrain severity. A future MM0xx code can be Error, Warning, or Info; a future MM1xx code is typically but not exclusively Info; a future MM2xx code (introduced under this convention, MM201 onward) is always a runtime exception, but the exception type is decided per-code (most will be `InvalidOperationException`, but a future MM2xx for a configuration error might be `ArgumentException`).
