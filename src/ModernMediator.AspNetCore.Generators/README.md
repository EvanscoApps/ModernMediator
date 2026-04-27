# ModernMediator.AspNetCore.Generators

Source generators for [ModernMediator.AspNetCore](https://www.nuget.org/packages/ModernMediator.AspNetCore). Generates Minimal API endpoint registrations from request handlers decorated with the `[Endpoint]` attribute.

This package is shipped transitively via the `ModernMediator.AspNetCore` package's project reference. **You normally do not install this package directly**. Installing `ModernMediator.AspNetCore` includes these generators automatically.

The generators produce a `MapMediatorEndpoints()` extension method on `IEndpointRouteBuilder` that registers all `[Endpoint]`-decorated handlers as Minimal API routes. See the [ModernMediator.AspNetCore README](https://www.nuget.org/packages/ModernMediator.AspNetCore) for usage details.

## License

MIT. See [LICENSE](https://github.com/evanscoapps/ModernMediator/blob/main/LICENSE) in the repository root.
