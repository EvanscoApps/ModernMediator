using System.Collections.Generic;
using System.Threading;

namespace ModernMediator
{
    /// <summary>
    /// Sends a streaming request and returns an async enumerable of responses.
    /// </summary>
    public interface IStreamer
    {
        /// <summary>
        /// Creates a stream of responses from a streaming request handler.
        /// </summary>
        /// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
        /// <param name="request">The streaming request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async enumerable of response items.</returns>
        IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
    }
}
