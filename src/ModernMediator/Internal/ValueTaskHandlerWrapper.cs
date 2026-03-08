using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator.Internal;

/// <summary>
/// Abstract base for cached per-request-type ValueTask pipeline wrappers.
/// Cached once per request type; eliminates all per-call reflection and boxing.
/// </summary>
internal abstract class ValueTaskHandlerWrapper<TResponse>
{
    public abstract ValueTask<TResponse> Handle(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
