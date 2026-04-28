# ModernMediator.FluentValidation

[FluentValidation](https://docs.fluentvalidation.net/) integration for [ModernMediator](https://www.nuget.org/packages/ModernMediator). Adds a `ValidationBehavior` pipeline behavior that runs registered `IValidator<TRequest>` validators before the request handler executes, throwing `ModernValidationException` if any validation rule fails.

## Installation

```bash
dotnet add package ModernMediator.FluentValidation
```

The package depends on [FluentValidation](https://www.nuget.org/packages/FluentValidation) 11.11.0 and [ModernMediator](https://www.nuget.org/packages/ModernMediator), which are pulled in transitively.

## Setup

The recommended pattern is to register validation inside the `AddModernMediator` configuration lambda, which lets you control exactly where `ValidationBehavior<,>` runs relative to other pipeline behaviors:

```csharp
using System.Reflection;
using ModernMediator;
using ModernMediator.FluentValidation;

builder.Services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();

    config.AddLogging();
    config.AddModernMediatorValidation(typeof(Program).Assembly);
    config.AddTimeout();
});
```

In this example, validation runs after logging and before timeout enforcement. Pipeline behaviors execute in registration order, so the position of `AddModernMediatorValidation` in the lambda determines its position in the pipeline.

`AddModernMediatorValidation` scans the supplied assembly (or assemblies) for `IValidator<TRequest>` implementations, registers them in the service container via FluentValidation's `AddValidatorsFromAssemblies`, and registers `ValidationBehavior<TRequest, TResponse>` as an open-generic `IPipelineBehavior` at the configuration's current `BehaviorLifetime` (defaults to transient).

Four overloads are available, two on `MediatorConfiguration` and two on `IServiceCollection`:

```csharp
// MediatorConfiguration overloads (recommended): position-aware, run inside AddModernMediator lambda
config.AddModernMediatorValidation(typeof(Program).Assembly);
config.AddModernMediatorValidation(assembly1, assembly2);
config.AddModernMediatorValidation(); // defaults to calling assembly

// IServiceCollection overloads: registered after AddModernMediator returns, validation runs last
services.AddModernMediatorValidation(typeof(Program).Assembly);
services.AddModernMediatorValidation(assembly1, assembly2);
services.AddModernMediatorValidation(); // defaults to calling assembly
```

The `IServiceCollection` overloads register validation after every behavior configured inside the `AddModernMediator` lambda. Use them when pipeline position does not matter or when your DI setup keeps registrations outside the configuration lambda for architectural reasons.

## Defining validators

Validators are written using FluentValidation's standard `AbstractValidator<T>`:

```csharp
using FluentValidation;
using ModernMediator;

public sealed record CreateOrderCommand(string CustomerEmail, decimal TotalAmount) : IRequest<OrderResult>;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.TotalAmount).GreaterThan(0m);
    }
}
```

When `CreateOrderCommand` is dispatched, every registered `IValidator<CreateOrderCommand>` runs in parallel via `Task.WhenAll`. If any validator reports failures, `ValidationBehavior` aggregates them and throws `ModernValidationException`. The handler does not execute.

## Handling validation failures

`ModernValidationException` exposes the failing rules through a single property:

```csharp
public IReadOnlyList<ValidationFailure> Errors { get; }
```

`ValidationFailure` is FluentValidation's own type (`FluentValidation.Results.ValidationFailure`), so each entry carries `PropertyName`, `ErrorMessage`, `ErrorCode`, `AttemptedValue`, and the standard FluentValidation metadata.

In ASP.NET Core, a typical pattern is to translate the exception into a 400 Bad Request response with a `ProblemDetails` payload. ModernMediator's exception-handler pipeline can carry this concern; see the [core ModernMediator README](https://github.com/evanscoapps/ModernMediator) for the exception-handler pattern.

```csharp
catch (ModernValidationException ex)
{
    var problems = ex.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    return Results.ValidationProblem(problems);
}
```

## Pipeline ordering

`ValidationBehavior` is registered as an open-generic `IPipelineBehavior<,>`. Pipeline behaviors execute in registration order: the first behavior registered runs outermost, the last runs innermost (closest to the handler). When using the `MediatorConfiguration` overload of `AddModernMediatorValidation`, the call's textual position inside the `AddModernMediator` lambda determines the behavior's pipeline position. When using the `IServiceCollection` overload, validation runs after every behavior registered inside the lambda. See the recommended registration order table in the [core ModernMediator README](https://github.com/evanscoapps/ModernMediator).

## See also

- [Core ModernMediator README](https://github.com/evanscoapps/ModernMediator) for the full library reference and pipeline behavior documentation
- [FluentValidation documentation](https://docs.fluentvalidation.net/) for the validator authoring reference

## License

MIT. See [LICENSE](https://github.com/evanscoapps/ModernMediator/blob/main/LICENSE) in the repository root.
