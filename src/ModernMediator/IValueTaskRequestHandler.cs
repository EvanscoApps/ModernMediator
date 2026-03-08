using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Defines a handler for a request that returns a response as a <see cref="ValueTask{TResponse}"/>.
    /// Use this when the handler can complete synchronously on hot paths (e.g., cache hits)
    /// to avoid the overhead of allocating a <see cref="Task{TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of response returned.</typeparam>
    public interface IValueTaskRequestHandler<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request and returns a response.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response wrapped in a ValueTask.</returns>
        ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }
}
