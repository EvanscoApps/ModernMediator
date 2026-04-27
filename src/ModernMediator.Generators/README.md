# ModernMediator.Generators

Source generators for [ModernMediator](https://www.nuget.org/packages/ModernMediator). Eliminates runtime reflection on the request-dispatch path and enables Native AOT compilation.

This package is shipped transitively via the core `ModernMediator` package's project reference. **You normally do not install this package directly** — installing `ModernMediator` includes these generators automatically.

If you are building a project that explicitly opts into the source-generated dispatch path (`AddModernMediatorGenerated()` rather than `AddModernMediator()` with assembly scanning), you may want this package referenced explicitly to ensure the generators are active during compilation. In most cases, the transitive reference from the core package is sufficient.

## Diagnostics

The generators emit compile-time diagnostics with the prefix `MM` (for example, `MM001` flags duplicate handler registrations). See the [core ModernMediator README](https://github.com/evanscoapps/ModernMediator) for the full diagnostic catalog.

## License

MIT — see [LICENSE](https://github.com/evanscoapps/ModernMediator/blob/main/LICENSE) in the repository root.
