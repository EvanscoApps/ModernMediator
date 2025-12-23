using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Handles exceptions thrown during request processing.
    /// Provides a clean way to handle specific exception types without wrapping behaviors in try/catch.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="TException">The exception type to handle.</typeparam>
    /// <example>
    /// <code>
    /// public class NotFoundExceptionHandler : IRequestExceptionHandler&lt;GetUserQuery, UserDto, NotFoundException&gt;
    /// {
    ///     public Task&lt;ExceptionHandlingResult&lt;UserDto&gt;&gt; Handle(
    ///         GetUserQuery request,
    ///         NotFoundException exception,
    ///         CancellationToken cancellationToken)
    ///     {
    ///         // Return a default response instead of throwing
    ///         return Task.FromResult(ExceptionHandlingResult&lt;UserDto&gt;.Handle(UserDto.Empty));
    ///         
    ///         // Or let it bubble up
    ///         // return Task.FromResult(ExceptionHandlingResult&lt;UserDto&gt;.NotHandled());
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
        where TRequest : IRequest<TResponse>
        where TException : Exception
    {
        /// <summary>
        /// Handles an exception thrown during request processing.
        /// </summary>
        /// <param name="request">The request that caused the exception.</param>
        /// <param name="exception">The exception that was thrown.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A result indicating whether the exception was handled.
        /// If handled, the response is returned to the caller.
        /// If not handled, the exception continues to bubble up.
        /// </returns>
        Task<ExceptionHandlingResult<TResponse>> Handle(
            TRequest request,
            TException exception,
            CancellationToken cancellationToken);
    }
}