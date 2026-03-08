using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator.Internal;

/// <summary>
/// Abstract base for cached per-request-type pipeline wrappers.
/// Cached once per request type; eliminates all per-call reflection and boxing.
/// </summary>
internal abstract class RequestHandlerWrapper<TResponse>
{
    public abstract Task<TResponse> Handle(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);

    public abstract Task ExecutePreProcessors(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);

    public abstract Task ExecutePostProcessors(
        IRequest<TResponse> request,
        TResponse response,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
