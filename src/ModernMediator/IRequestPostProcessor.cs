using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Defines a post-processor that runs after the request handler.
    /// Use this for logging, caching responses, notifications, etc.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <example>
    /// <code>
    /// public class CachingPostProcessor&lt;TRequest, TResponse&gt; : IRequestPostProcessor&lt;TRequest, TResponse&gt;
    ///     where TRequest : IRequest&lt;TResponse&gt;
    /// {
    ///     public Task Process(TRequest request, TResponse response, CancellationToken ct)
    ///     {
    ///         // Cache the response
    ///         _cache.Set(request, response);
    ///         return Task.CompletedTask;
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IRequestPostProcessor<in TRequest, in TResponse>
    {
        /// <summary>
        /// Process the request after the handler has executed.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="response">The response from the handler.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
    }
}