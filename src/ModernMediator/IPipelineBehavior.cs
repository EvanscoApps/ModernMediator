using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Delegate representing the next action in the pipeline.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <returns>The response from the next behavior or handler.</returns>
    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

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
    ///     public async Task&lt;TResponse&gt; Handle(TRequest request, RequestHandlerDelegate&lt;TResponse&gt; next, CancellationToken ct)
    ///     {
    ///         _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
    ///         var response = await next();
    ///         _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
    ///         return response;
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IPipelineBehavior<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request, optionally calling the next behavior or handler in the pipeline.
        /// </summary>
        /// <param name="request">The request being handled.</param>
        /// <param name="next">Delegate to invoke the next behavior or the handler.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response.</returns>
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
    }
}