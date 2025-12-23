using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Base class for exception handlers that simplifies implementation.
    /// Provides helper properties for returning handled/not-handled results.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="TException">The exception type to handle.</typeparam>
    /// <example>
    /// <code>
    /// public class ValidationExceptionHandler : RequestExceptionHandler&lt;CreateUserCommand, Unit, ValidationException&gt;
    /// {
    ///     private readonly ILogger _logger;
    ///     
    ///     public ValidationExceptionHandler(ILogger logger) => _logger = logger;
    ///     
    ///     protected override Task&lt;ExceptionHandlingResult&lt;Unit&gt;&gt; Handle(
    ///         CreateUserCommand request,
    ///         ValidationException exception,
    ///         CancellationToken cancellationToken)
    ///     {
    ///         _logger.LogWarning(exception, "Validation failed for {Request}", request);
    ///         
    ///         // Let exception bubble up after logging
    ///         return NotHandled;
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class RequestExceptionHandler<TRequest, TResponse, TException>
        : IRequestExceptionHandler<TRequest, TResponse, TException>
        where TRequest : IRequest<TResponse>
        where TException : Exception
    {
        /// <summary>
        /// Returns a result indicating the exception was not handled.
        /// The exception will continue to bubble up.
        /// </summary>
        protected static Task<ExceptionHandlingResult<TResponse>> NotHandled =>
            Task.FromResult(ExceptionHandlingResult<TResponse>.NotHandled());

        /// <summary>
        /// Returns a result indicating the exception was handled with the given response.
        /// The response will be returned to the caller instead of throwing.
        /// </summary>
        /// <param name="response">The response to return.</param>
        protected static Task<ExceptionHandlingResult<TResponse>> Handled(TResponse response) =>
            Task.FromResult(ExceptionHandlingResult<TResponse>.Handle(response));

        /// <summary>
        /// Handles an exception thrown during request processing.
        /// </summary>
        protected abstract Task<ExceptionHandlingResult<TResponse>> Handle(
            TRequest request,
            TException exception,
            CancellationToken cancellationToken);

        Task<ExceptionHandlingResult<TResponse>> IRequestExceptionHandler<TRequest, TResponse, TException>.Handle(
            TRequest request,
            TException exception,
            CancellationToken cancellationToken) =>
            Handle(request, exception, cancellationToken);
    }
}