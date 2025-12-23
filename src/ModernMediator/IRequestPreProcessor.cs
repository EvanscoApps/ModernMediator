using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Defines a pre-processor that runs before the request handler.
    /// Use this for validation, logging, authorization checks, etc.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <example>
    /// <code>
    /// public class ValidationPreProcessor&lt;TRequest&gt; : IRequestPreProcessor&lt;TRequest&gt;
    ///     where TRequest : IRequest
    /// {
    ///     public Task Process(TRequest request, CancellationToken ct)
    ///     {
    ///         // Validate request
    ///         if (request is IValidatable validatable)
    ///             validatable.Validate();
    ///         return Task.CompletedTask;
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IRequestPreProcessor<in TRequest>
    {
        /// <summary>
        /// Process the request before the handler executes.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task Process(TRequest request, CancellationToken cancellationToken);
    }
}