using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModernMediator.Dispatchers;

namespace ModernMediator
{
    /// <summary>
    /// Modern mediator interface for loosely-coupled messaging.
    /// 
    /// USAGE (in order of preference):
    /// 1. DI Container: services.AddModernMediator();
    /// 2. Singleton:    Mediator.Instance
    /// 3. Factory:      Mediator.Create()  (for isolated instances)
    /// </summary>
    public interface IMediator : IDisposable
    {
        #region Request/Response

        /// <summary>
        /// Sends a request to a single handler and returns the response.
        /// Unlike Publish, this expects exactly one handler to be registered.
        /// </summary>
        /// <typeparam name="TResponse">The type of response expected.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response from the handler.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no handler or multiple handlers are registered.</exception>
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

        #endregion

        #region Streaming

        /// <summary>
        /// Creates a stream of responses from a streaming request handler.
        /// Use this for scenarios requiring multiple results over time, such as
        /// pagination, real-time feeds, or large dataset processing.
        /// </summary>
        /// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
        /// <param name="request">The streaming request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async enumerable of response items.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no handler is registered.</exception>
        /// <example>
        /// <code>
        /// await foreach (var user in mediator.CreateStream(new GetAllUsersRequest(), cancellationToken))
        /// {
        ///     Console.WriteLine(user.Name);
        /// }
        /// </code>
        /// </example>
        IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

        #endregion

        #region Pub/Sub (Notifications)

        /// <summary>
        /// Subscribe to messages of type T with optional filtering.
        /// </summary>
        /// <param name="handler">The handler to invoke when a message is received.</param>
        /// <param name="weak">If true (default), uses weak reference to allow garbage collection.</param>
        /// <param name="filter">Optional predicate to filter messages.</param>
        /// <returns>A disposable token to unsubscribe.</returns>
        IDisposable Subscribe<T>(Action<T> handler, bool weak = true, Predicate<T>? filter = null);

        /// <summary>
        /// Subscribe to async messages of type T with optional filtering (true async handlers).
        /// </summary>
        IDisposable SubscribeAsync<T>(Func<T, Task> handler, bool weak = true, Predicate<T>? filter = null);

        /// <summary>
        /// Subscribe to messages on the UI thread (requires IDispatcher via SetDispatcher).
        /// </summary>
        IDisposable SubscribeOnMainThread<T>(Action<T> handler, bool weak = true, Predicate<T>? filter = null);

        /// <summary>
        /// Subscribe to async messages on the UI thread (requires IDispatcher via SetDispatcher).
        /// </summary>
        IDisposable SubscribeAsyncOnMainThread<T>(Func<T, Task> handler, bool weak = true, Predicate<T>? filter = null);

        /// <summary>
        /// Subscribe to messages by string key.
        /// </summary>
        IDisposable Subscribe<T>(string key, Action<T> handler, bool weak = true, Predicate<T>? filter = null);

        /// <summary>
        /// Publish a message synchronously. Returns true if at least one handler was invoked.
        /// Null messages are allowed (returns false as no-op).
        /// </summary>
        bool Publish<T>(T? message);

        /// <summary>
        /// Publish a message by string key synchronously. Returns true if at least one handler was invoked.
        /// Null messages are allowed (returns false as no-op).
        /// </summary>
        bool Publish<T>(string key, T? message);

        /// <summary>
        /// Publish a message asynchronously (background thread wrapper over sync publish).
        /// NOTE: This wraps synchronous handlers in Task.Run, not true async handler invocation.
        /// Use PublishAsyncTrue for true async handlers.
        /// </summary>
        Task<bool> PublishAsync<T>(T? message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish a message by string key asynchronously (background thread wrapper over sync publish).
        /// </summary>
        Task<bool> PublishAsync<T>(string key, T? message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish a message to async handlers with true async invocation.
        /// Awaits all handlers with Task.WhenAll.
        /// </summary>
        Task<bool> PublishAsyncTrue<T>(T? message, CancellationToken cancellationToken = default);

        #endregion

        #region Configuration

        /// <summary>
        /// Set the dispatcher for main thread marshalling.
        /// </summary>
        void SetDispatcher(IDispatcher dispatcher);

        /// <summary>
        /// Event raised when a handler throws an exception.
        /// CRITICAL: Subscribe to this in production when using LogAndContinue policy!
        /// </summary>
        event EventHandler<HandlerErrorEventArgs>? HandlerError;

        /// <summary>
        /// Current error handling policy.
        /// </summary>
        ErrorPolicy ErrorPolicy { get; set; }

        #endregion
    }
}