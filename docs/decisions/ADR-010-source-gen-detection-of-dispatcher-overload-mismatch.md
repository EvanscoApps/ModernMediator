# ADR-010: Source-gen detection of dispatcher overload mismatch

**Status:** Accepted
**Date:** 2026-04-29
**Component:** ModernMediator.Generators

---

## Context

ADR-009 introduced a runtime check (MM200) for the case where a request handler is registered under one dispatch interface (`IRequestHandler` or `IValueTaskRequestHandler`) and the consumer's call site invokes the dispatcher via the other path (`Send` or `SendAsync`). The runtime check fires the first time the mismatched code path executes; the wrapper performs a secondary `IServiceProvider.GetService` against the alternate interface and, when a registration is found, throws `InvalidOperationException` with a guiding `[MM200]` message.

ADR-009's Consequences section noted that compile-time detection of the same condition was a separate work item, deferred from v2.2 because the existing source generator does not model dispatcher call sites. This ADR documents that follow-up work and its design.

The investigation in advance of this ADR confirmed that the existing analyzer infrastructure is the natural channel for the diagnostic. `WeakLambdaSubscriptionAnalyzer` (MM008) is a `DiagnosticAnalyzer` that walks `InvocationExpressionSyntax` nodes, resolves the called method's symbol, and tests properties of the receiver and arguments. The dispatcher overload mismatch detector is structurally analogous: walk invocations, filter to `Send` / `SendAsync` on `IMediator` / `ISender`, recover the request type from the argument, and compare against a registry of handler types built once per compilation.

The source generator's existing handler registry is in-memory state of the `IIncrementalGenerator` and is not accessible from a separate `DiagnosticAnalyzer`. The analyzer maintains its own registry. Because the registry construction touches every type in the compilation, it is built once via `RegisterCompilationStartAction` and the per-invocation `RegisterSyntaxNodeAction` callback queries the prebuilt dictionary.

## Decision

A new `DiagnosticAnalyzer`, `DispatcherOverloadMismatchAnalyzer`, is added to `ModernMediator.Generators`. It emits MM009 (Warning) at the call site when the consumer invokes `Send` for a request whose handler is registered as `IValueTaskRequestHandler`, or `SendAsync` for a request whose handler is registered as `IRequestHandler`. The diagnostic does not fire when no handler is registered (MM002 covers that case), nor when both interfaces are registered for the same request type (either dispatch path is valid).

MM009 occupies the next available slot in the MM0xx range reserved for compile-time analyzer diagnostics per ADR-008. The runtime code MM200 (ADR-009) remains in the MM2xx slot. Both codes surface the same condition; the codes are intentionally distinct because the channels are distinct.

The analyzer's structure:

- `RegisterCompilationStartAction` builds a `Dictionary<ITypeSymbol, (bool hasTask, bool hasValueTask)>` keyed by request type. The compilation walk identifies every concrete type whose `AllInterfaces` includes `IRequestHandler<,>` or `IValueTaskRequestHandler<,>` and sets the corresponding flag for the request type (the first generic argument).
- `RegisterSyntaxNodeAction` on `InvocationExpressionSyntax` filters first by syntactic method name (`Send` or `SendAsync`), then resolves the `IMethodSymbol` and confirms the containing type is `IMediator`, `ISender`, or a generated extension method on those interfaces. The first argument's type is the request type; it is recovered via `SemanticModel.GetTypeInfo`.
- The registry lookup yields one of four states. Both flags true: legal; return. Neither flag set (request not in registry): no handler at all; return so MM002 covers it. Task-only with a `Send` call, or ValueTask-only with a `SendAsync` call: correct dispatch; return. Mismatch: emit MM009 with the request type name, the registered interface, and the suggested method.

## Rationale

Two alternative shapes were considered.

The first was to extend `MediatorGenerator` (the `IIncrementalGenerator`) to walk invocations as well as type declarations. This was rejected because the generator's purpose is to model handler declarations and produce registration code; adding a parallel call-site walk inflates the generator's responsibility surface. `DiagnosticAnalyzer` is the established channel for compilation-scoped analysis of consumer invocation expressions; `WeakLambdaSubscriptionAnalyzer` is the in-tree precedent. Keeping the two channels separate matches Roslyn's own architectural split.

The second was to fold the diagnostic into MM200 directly: emit a Roslyn diagnostic with id `"MM200"` from the analyzer alongside the runtime exception's bracketed prefix. This was rejected because ADR-008 reserves MM2xx for runtime codes specifically; mixing a Roslyn diagnostic id into the runtime slot would invalidate the slot convention's signal value. A user who searches for `MM009` in their build output finds the source-gen detector; a user who searches for `MM200` in an exception finds the runtime check. The two codes carry the same prose meaning but distinct provenance, which is useful for users tracing how the issue surfaced.

The Warning severity (rather than Error) matches the convention established by MM002 and MM006: the consumer's code compiles cleanly, the issue surfaces only at dispatch, and forcing an error would break consumers who upgrade and have not yet adjusted their call sites. Warnings are visible in the IDE error list and in CI build output without halting compilation. Consumers who want stricter enforcement can promote MM009 to error via `<WarningsAsErrors>` or an `.editorconfig` severity override.

The decision to build the registry once via `CompilationStartAction` rather than on every invocation is a standard analyzer performance pattern. The registry construction cost (one full type-symbol walk) is amortized across every `Send` / `SendAsync` invocation in the compilation. The per-invocation callback's hot path is one dictionary lookup keyed by `SymbolEqualityComparer.Default`, which is cheap.

## Consequences

Consumers see overload mismatch at compile time in the IDE error list, before the runtime check ever fires. The analyzer's reach is limited to types visible to the compilation: a handler registered in the same project or in a project reference is detected; a handler registered in a precompiled NuGet dependency may not be, depending on how the consumer has structured registration. The runtime MM200 check remains in place precisely to cover the cross-assembly case.

Consumers who already have well-formed dispatch code see no MM009 noise; the negative tests confirm that correct dispatch, no-handler-registered, and dual-registration cases all suppress the diagnostic.

The `MediatorGenerator` source generator is unchanged. The analyzer is independent infrastructure; the existing nine descriptors (MM001 through MM008, MM100) and their generator-side emission paths are untouched. The structural gate test `AllDiagnosticDescriptors_HaveTests` is extended to include MM009 in its tested-ids set; the six new tests in `AnalyzerTests` satisfy the positive/negative coverage requirement.

Future runtime/analyzer pairs follow this pattern: a runtime code in MM2xx for the cross-assembly safety net, a compile-time code in MM0xx for the in-source detection, and an ADR documenting the relationship. The two-code design surfaces the channel from the code itself, which is the property ADR-008 was designed to preserve.
