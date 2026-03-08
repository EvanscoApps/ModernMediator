using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Delegate representing the next step in the pipeline.
    /// Passing request and cancellationToken explicitly eliminates closure allocation
    /// on every dispatch.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request being handled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the next behavior or handler.</returns>
    public delegate Task<TResponse> RequestHandlerDelegate<TRequest, TResponse>(
        TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Pipeline behavior that wraps request handler execution.
    /// Use this for cross-cutting concerns like logging, validation, caching, authorization, etc.
    /// Behaviors execute in the order they are registered, forming a pipeline around the handler.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <example>
    /// <code>
    /// public class LoggingBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
    ///     where TRequest : IRequest&lt;TResponse&gt;
    /// {
    ///     public async Task&lt;TResponse&gt; Handle(TRequest request, RequestHandlerDelegate&lt;TRequest, TResponse&gt; next, CancellationToken ct)
    ///     {
    ///         _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
    ///         var response = await next(request, ct);
    ///         _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
    ///         return response;
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request, optionally calling the next behavior or handler in the pipeline.
        /// </summary>
        /// <param name="request">The request being handled.</param>
        /// <param name="next">Delegate to invoke the next behavior or the handler. Pass request and cancellationToken explicitly.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response.</returns>
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken);
    }
}
