using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator.Internal;

/// <summary>
/// Fully-typed pipeline wrapper for <typeparamref name="TRequest"/> → <typeparamref name="TResponse"/>.
/// Generic type constraints eliminate all runtime casts and boxing. Cached once per request type.
/// Uses index-based recursion instead of per-behavior closure allocation.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        var handler = (IRequestHandler<TRequest, TResponse>?)
            serviceProvider.GetService(typeof(IRequestHandler<TRequest, TResponse>));
        if (handler is null)
        {
            // MM200: secondary lookup for the alternate dispatch interface so that an
            // overload mismatch (handler registered as IValueTaskRequestHandler but caller
            // invoked Send) surfaces as a guiding message instead of a generic "no handler"
            // error. Runs only on the error path; no overhead for successful dispatches.
            var valueTaskHandler = serviceProvider.GetService(typeof(IValueTaskRequestHandler<TRequest, TResponse>));
            if (valueTaskHandler is not null)
            {
                throw new InvalidOperationException(
                    $"[MM200] No IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> is registered, " +
                    $"but an IValueTaskRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> is registered. " +
                    "Did you mean to call SendAsync instead of Send?");
            }
            throw new InvalidOperationException(
                $"No handler registered for request type {typeof(TRequest).Name}. " +
                $"Register a handler implementing IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> " +
                "using AddModernMediator() with assembly scanning or manual registration.");
        }

        var behaviors = (IEnumerable<IPipelineBehavior<TRequest, TResponse>>?)
            serviceProvider.GetService(typeof(IEnumerable<IPipelineBehavior<TRequest, TResponse>>));

        if (behaviors == null)
            return handler.Handle(typedRequest, cancellationToken);

        // Materialize into array for index-based recursion (no per-behavior closures).
        // Array is in registration order — index 0 is outermost, last is innermost.
        var array = behaviors as IPipelineBehavior<TRequest, TResponse>[] ?? behaviors.ToArray();
        if (array.Length == 0)
            return handler.Handle(typedRequest, cancellationToken);

        return ExecutePipeline(typedRequest, handler, array, array.Length, 0, cancellationToken);
    }

    private static Task<TResponse> ExecutePipeline(
        TRequest request,
        IRequestHandler<TRequest, TResponse> handler,
        IPipelineBehavior<TRequest, TResponse>[] behaviors,
        int count,
        int index,
        CancellationToken cancellationToken)
    {
        if (index == count)
            return handler.Handle(request, cancellationToken);

        var behavior = behaviors[index];
        return behavior.Handle(
            request,
            (req, ct) => ExecutePipeline(req, handler, behaviors, count, index + 1, ct),
            cancellationToken);
    }

    public override async Task ExecutePreProcessors(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var preProcessors = (IEnumerable<IRequestPreProcessor<TRequest>>?)
            serviceProvider.GetService(typeof(IEnumerable<IRequestPreProcessor<TRequest>>));
        if (preProcessors == null) return;

        var typedRequest = (TRequest)request;
        foreach (var p in preProcessors)
            await p.Process(typedRequest, cancellationToken).ConfigureAwait(false);
    }

    public override async Task ExecutePostProcessors(
        IRequest<TResponse> request,
        TResponse response,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var postProcessors = (IEnumerable<IRequestPostProcessor<TRequest, TResponse>>?)
            serviceProvider.GetService(typeof(IEnumerable<IRequestPostProcessor<TRequest, TResponse>>));
        if (postProcessors == null) return;

        var typedRequest = (TRequest)request;
        foreach (var p in postProcessors)
            await p.Process(typedRequest, response, cancellationToken).ConfigureAwait(false);
    }
}
