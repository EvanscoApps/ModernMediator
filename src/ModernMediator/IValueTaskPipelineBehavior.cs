using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Delegate representing the next step in a ValueTask pipeline.
    /// Passing request and cancellationToken explicitly eliminates closure allocation.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request being handled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the next behavior or handler.</returns>
    public delegate ValueTask<TResponse> ValueTaskRequestHandlerDelegate<TRequest, TResponse>(
        TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Pipeline behavior that wraps request handler execution using ValueTask.
    /// Use this for cross-cutting concerns where the entire pipeline should remain
    /// allocation-free on synchronous completion paths.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public interface IValueTaskPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request, optionally calling the next behavior or handler in the pipeline.
        /// </summary>
        /// <param name="request">The request being handled.</param>
        /// <param name="next">Delegate to invoke the next behavior or the handler.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response wrapped in a ValueTask.</returns>
        ValueTask<TResponse> Handle(TRequest request, ValueTaskRequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken);
    }
}
