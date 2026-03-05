using FluentValidation;
using ModernMediator;
using ModernMediator.AspNetCore;

namespace ModernMediator.Sample.WebApi.Requests;

// ── POST with Result<T> + FluentValidation ───────────────────

[Endpoint("/api/items")]
public record CreateItem(string Name, decimal Price) : IRequest<Result<ItemCreated>>;

public record ItemCreated(int Id, string Name, decimal Price);

public class CreateItemHandler : IRequestHandler<CreateItem, Result<ItemCreated>>
{
    private static int _nextId;

    public Task<Result<ItemCreated>> Handle(CreateItem request, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var item = new ItemCreated(id, request.Name, request.Price);
        return Task.FromResult(Result<ItemCreated>.Success(item));
    }
}

// FluentValidation validator — automatically discovered by assembly scan
public class CreateItemValidator : AbstractValidator<CreateItem>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
