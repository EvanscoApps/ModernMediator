using System;
using System.Collections.Generic;
using System.Threading;

namespace ModernMediator.Internal;

/// <summary>
/// Fully-typed stream pipeline wrapper for <typeparamref name="TRequest"/> → <typeparamref name="TResponse"/>.
/// Generic type constraints eliminate all runtime reflection. Cached once per request type.
/// </summary>
internal sealed class StreamHandlerWrapperImpl<TRequest, TResponse> : StreamHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public override IAsyncEnumerable<TResponse> Handle(
        IStreamRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        var handler = (IStreamRequestHandler<TRequest, TResponse>?)
            serviceProvider.GetService(typeof(IStreamRequestHandler<TRequest, TResponse>))
            ?? throw new InvalidOperationException(
                $"No stream handler registered for request type {typeof(TRequest).Name}. " +
                $"Register a handler implementing IStreamRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> " +
                "using AddModernMediator() with assembly scanning or manual registration.");

        return handler.Handle(typedRequest, cancellationToken);
    }
}
