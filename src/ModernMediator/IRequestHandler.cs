using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Defines a handler for a request that returns a response.
    /// Each request type should have exactly one handler.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of response returned.</typeparam>
    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request and returns a response.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response.</returns>
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines a handler for a request that returns no meaningful value.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being handled.</typeparam>
    public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
        where TRequest : IRequest<Unit>
    {
    }
}