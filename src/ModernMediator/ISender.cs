using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Sends a request through the mediator pipeline and returns a response.
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Sends a request to a single handler and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of response expected.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response from the handler.</returns>
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request to a ValueTask handler and returns the response without allocating a Task.
        /// This is the fully zero-allocation path for handlers that implement
        /// <see cref="IValueTaskRequestHandler{TRequest, TResponse}"/>.
        /// </summary>
        /// <typeparam name="TResponse">The type of response expected.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response from the handler wrapped in a ValueTask.</returns>
        ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    }
}
