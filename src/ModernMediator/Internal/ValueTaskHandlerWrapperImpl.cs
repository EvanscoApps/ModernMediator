using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator.Internal;

/// <summary>
/// Fully-typed ValueTask pipeline wrapper for <typeparamref name="TRequest"/> → <typeparamref name="TResponse"/>.
/// ValueTask flows through the entire pipeline without .AsTask() allocation.
/// Uses index-based recursion to eliminate per-behavior closure allocation.
/// </summary>
internal sealed class ValueTaskHandlerWrapperImpl<TRequest, TResponse> : ValueTaskHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override ValueTask<TResponse> Handle(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        var handler = (IValueTaskRequestHandler<TRequest, TResponse>?)
            serviceProvider.GetService(typeof(IValueTaskRequestHandler<TRequest, TResponse>));
        if (handler is null)
        {
            // MM201: secondary lookup for the alternate dispatch interface so that an
            // overload mismatch (handler registered as IRequestHandler but caller invoked
            // SendAsync) surfaces as a guiding message instead of a generic "no handler"
            // error. Runs only on the error path; no overhead for successful dispatches.
            var taskHandler = serviceProvider.GetService(typeof(IRequestHandler<TRequest, TResponse>));
            if (taskHandler is not null)
            {
                throw new InvalidOperationException(
                    $"[MM201] No IValueTaskRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> is registered, " +
                    $"but an IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> is registered. " +
                    "Did you mean to call Send instead of SendAsync?");
            }
            throw new InvalidOperationException(
                $"No ValueTask handler registered for request type {typeof(TRequest).Name}. " +
                $"Register a handler implementing IValueTaskRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}> " +
                "using AddModernMediator() with assembly scanning or manual registration.");
        }

        var behaviors = (IEnumerable<IValueTaskPipelineBehavior<TRequest, TResponse>>?)
            serviceProvider.GetService(typeof(IEnumerable<IValueTaskPipelineBehavior<TRequest, TResponse>>));

        if (behaviors == null)
            return handler.Handle(typedRequest, cancellationToken);

        var array = behaviors as IValueTaskPipelineBehavior<TRequest, TResponse>[] ?? behaviors.ToArray();
        if (array.Length == 0)
            return handler.Handle(typedRequest, cancellationToken);

        return ExecutePipeline(typedRequest, handler, array, array.Length, 0, cancellationToken);
    }

    private static ValueTask<TResponse> ExecutePipeline(
        TRequest request,
        IValueTaskRequestHandler<TRequest, TResponse> handler,
        IValueTaskPipelineBehavior<TRequest, TResponse>[] behaviors,
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
}
