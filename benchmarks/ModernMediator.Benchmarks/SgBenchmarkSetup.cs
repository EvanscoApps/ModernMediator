// ── martinothamar/Mediator (source-generated) handler types ──
// Prefix: Sg (Source-Generated)
// All types use global::Mediator.* to avoid conflict with ModernMediator.Mediator class.

using Microsoft.Extensions.DependencyInjection;

[assembly: global::Mediator.MediatorOptions(
    Namespace = "SgMediator",
    ServiceLifetime = ServiceLifetime.Singleton)]

namespace ModernMediator.Benchmarks;

// ── Request/Response types ──────────────────────────────────

public record SgPingRequest(string Message) : global::Mediator.IRequest<SgPongResponse>;

public record SgPongResponse(string Message);

public class SgPingHandler : global::Mediator.IRequestHandler<SgPingRequest, SgPongResponse>
{
    public ValueTask<SgPongResponse> Handle(SgPingRequest request, CancellationToken ct)
        => new(new SgPongResponse(request.Message));
}

// ── Notification types ──────────────────────────────────────

public record SgPingNotification(string Message) : global::Mediator.INotification;

public class SgNotificationHandler1 : global::Mediator.INotificationHandler<SgPingNotification>
{
    public ValueTask Handle(SgPingNotification notification, CancellationToken ct)
        => default;
}

public class SgNotificationHandler2 : global::Mediator.INotificationHandler<SgPingNotification>
{
    public ValueTask Handle(SgPingNotification notification, CancellationToken ct)
        => default;
}

public class SgNotificationHandler3 : global::Mediator.INotificationHandler<SgPingNotification>
{
    public ValueTask Handle(SgPingNotification notification, CancellationToken ct)
        => default;
}

