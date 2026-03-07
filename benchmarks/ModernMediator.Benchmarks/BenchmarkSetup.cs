namespace ModernMediator.Benchmarks;

// ── ModernMediator request/response types ────────────────────

public record MmPingRequest(string Message) : IRequest<MmPongResponse>;

public record MmPongResponse(string Message);

public class MmPingHandler : IRequestHandler<MmPingRequest, MmPongResponse>
{
    public Task<MmPongResponse> Handle(MmPingRequest request, CancellationToken ct = default)
        => Task.FromResult(new MmPongResponse(request.Message));
}

// ── ModernMediator notification types ────────────────────────

public record MmPingNotification(string Message) : INotification;

public class MmNotificationHandler1 : INotificationHandler<MmPingNotification>
{
    public Task Handle(MmPingNotification notification, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class MmNotificationHandler2 : INotificationHandler<MmPingNotification>
{
    public Task Handle(MmPingNotification notification, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class MmNotificationHandler3 : INotificationHandler<MmPingNotification>
{
    public Task Handle(MmPingNotification notification, CancellationToken ct = default)
        => Task.CompletedTask;
}

// ── ModernMediator pipeline behavior ─────────────────────────

public class MmLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken ct) => next();
}

// ── MediatR request/response types ───────────────────────────

public record MtrPingRequest(string Message) : MediatR.IRequest<MtrPongResponse>;

public record MtrPongResponse(string Message);

public class MtrPingHandler : MediatR.IRequestHandler<MtrPingRequest, MtrPongResponse>
{
    public Task<MtrPongResponse> Handle(MtrPingRequest request, CancellationToken ct)
        => Task.FromResult(new MtrPongResponse(request.Message));
}

// ── MediatR notification types ───────────────────────────────

public record MtrPingNotification(string Message) : MediatR.INotification;

public class MtrNotificationHandler1 : MediatR.INotificationHandler<MtrPingNotification>
{
    public Task Handle(MtrPingNotification notification, CancellationToken ct)
        => Task.CompletedTask;
}

public class MtrNotificationHandler2 : MediatR.INotificationHandler<MtrPingNotification>
{
    public Task Handle(MtrPingNotification notification, CancellationToken ct)
        => Task.CompletedTask;
}

public class MtrNotificationHandler3 : MediatR.INotificationHandler<MtrPingNotification>
{
    public Task Handle(MtrPingNotification notification, CancellationToken ct)
        => Task.CompletedTask;
}

// ── MediatR pipeline behavior ────────────────────────────────

public class MtrLoggingBehavior<TRequest, TResponse>
    : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct) => next();
}
