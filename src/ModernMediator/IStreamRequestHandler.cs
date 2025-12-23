using System.Collections.Generic;
using System.Threading;

namespace ModernMediator
{
    /// <summary>
    /// Defines a handler for a streaming request that yields multiple responses over time.
    /// </summary>
    /// <typeparam name="TRequest">The type of streaming request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
    /// <example>
    /// <code>
    /// public class GetAllUsersStreamHandler : IStreamRequestHandler&lt;GetAllUsersStreamRequest, UserDto&gt;
    /// {
    ///     public async IAsyncEnumerable&lt;UserDto&gt; Handle(
    ///         GetAllUsersStreamRequest request,
    ///         [EnumeratorCancellation] CancellationToken cancellationToken = default)
    ///     {
    ///         await foreach (var user in _database.GetUsersAsync(cancellationToken))
    ///         {
    ///             yield return new UserDto(user.Id, user.Name);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IStreamRequestHandler<in TRequest, out TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        /// <summary>
        /// Handles the streaming request and yields responses over time.
        /// </summary>
        /// <param name="request">The streaming request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async enumerable of response items.</returns>
        IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }
}