using System;
using System.Collections.Generic;
using System.Threading;

namespace ModernMediator.Internal;

/// <summary>
/// Abstract base for cached per-stream-request-type handler wrappers.
/// Cached once per request type; eliminates all per-call reflection.
/// </summary>
internal abstract class StreamHandlerWrapper<TResponse>
{
    public abstract IAsyncEnumerable<TResponse> Handle(
        IStreamRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
