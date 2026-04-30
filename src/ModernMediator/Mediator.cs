using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ModernMediator.Dispatchers;
using ModernMediator.Internal;

namespace ModernMediator
{
    /// <summary>
    /// Production-ready mediator for loosely-coupled messaging with support for
    /// weak references, async handlers, filters, pipeline behaviors, and UI thread dispatching.
    /// </summary>
    public sealed class Mediator : IMediator, IServiceProviderAccessor
    {
        private static readonly Lazy<IMediator> _instance = new Lazy<IMediator>(() => new Mediator());

        // Request/Response handlers (DI-registered)
        private IServiceProvider? _serviceProvider;
        private ISubscriberExceptionSink? _subscriberExceptionSink;
        private bool _sinkResolved;

        // Pub/Sub handlers
        private readonly ReaderWriterLockSlim _typeHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<Type, ImmutableArray<IHandlerEntry>> _typeHandlers =
            ImmutableDictionary<Type, ImmutableArray<IHandlerEntry>>.Empty;

        private readonly ReaderWriterLockSlim _asyncHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<Type, ImmutableArray<IAsyncHandlerEntry>> _asyncHandlers =
            ImmutableDictionary<Type, ImmutableArray<IAsyncHandlerEntry>>.Empty;

        // Callback handlers (sync handlers that return values)
        private readonly ReaderWriterLockSlim _callbackHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<Type, ImmutableArray<ICallbackHandlerEntry>> _callbackHandlers =
            ImmutableDictionary<Type, ImmutableArray<ICallbackHandlerEntry>>.Empty;

        // Async callback handlers (async handlers that return values)
        private readonly ReaderWriterLockSlim _asyncCallbackHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<Type, ImmutableArray<IAsyncCallbackHandlerEntry>> _asyncCallbackHandlers =
            ImmutableDictionary<Type, ImmutableArray<IAsyncCallbackHandlerEntry>>.Empty;

        private readonly ReaderWriterLockSlim _stringHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<string, (Type MessageType, ImmutableArray<IHandlerEntry> Handlers)> _stringHandlers =
            ImmutableDictionary<string, (Type, ImmutableArray<IHandlerEntry>)>.Empty;

        private int _subscriptionVersion;
        private readonly ConcurrentDictionary<(Type, int), (ImmutableArray<IHandlerEntry>, IReadOnlyList<Type>)> _typeMatchCache =
            new ConcurrentDictionary<(Type, int), (ImmutableArray<IHandlerEntry>, IReadOnlyList<Type>)>();

        private IDispatcher? _uiDispatcher;
        private ErrorPolicy _errorPolicy = ErrorPolicy.ContinueAndAggregate;
        private CachingMode _cachingMode = CachingMode.Eager;
        private bool _disposed;

        /// <summary>
        /// Cached fully-typed pipeline wrappers per request type. Allocated once per type; zero per-call overhead after first dispatch.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, object> _wrapperCache = new();

        /// <summary>
        /// Cached fully-typed stream handler wrappers per stream request type. One-time allocation per type.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, object> _streamWrapperCache = new();

        /// <summary>
        /// Cached fully-typed ValueTask pipeline wrappers per request type. One-time allocation per type.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, object> _valueTaskWrapperCache = new();

        /// <summary>
        /// Gets the singleton instance of the Mediator.
        /// For new applications, prefer dependency injection via AddModernMediator().
        /// For Pub/Sub with shared subscriptions across scopes, use this singleton.
        /// </summary>
        public static IMediator Instance => _instance.Value;

        /// <summary>
        /// Creates a new Mediator instance.
        /// For most applications, prefer dependency injection via AddModernMediator() or use Instance singleton.
        /// Use this factory when you need isolated Mediator instances (e.g., testing, modular architectures).
        /// </summary>
        /// <returns>A new IMediator instance.</returns>
        public static IMediator Create() => new Mediator();

        /// <summary>
        /// Creates a new Mediator instance with a service provider for resolving handlers.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving request handlers.</param>
        /// <returns>A new IMediator instance.</returns>
        public static IMediator Create(IServiceProvider serviceProvider)
        {
            var mediator = new Mediator();
            mediator._serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            return mediator;
        }

        /// <inheritdoc />
        public event EventHandler<HandlerErrorEventArgs>? HandlerError;

        /// <inheritdoc />
        public ErrorPolicy ErrorPolicy
        {
            get => _errorPolicy;
            set => _errorPolicy = value;
        }

        /// <summary>
        /// Creates a new Mediator instance without a service provider.
        /// Use Mediator.Create(), Mediator.Instance, or AddModernMediator() for DI instead.
        /// </summary>
        internal Mediator() { }

        /// <summary>
        /// Creates a new Mediator instance with the specified service provider.
        /// This constructor is used by dependency injection to provide the scoped service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving request handlers and behaviors.</param>
        public Mediator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Sets the service provider for resolving request handlers.
        /// Called internally by DI registration.
        /// </summary>
        internal void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Sets the caching mode for handler initialization.
        /// Called internally by DI registration.
        /// </summary>
        internal void SetCachingMode(CachingMode cachingMode)
        {
            _cachingMode = cachingMode;
        }

        /// <summary>
        /// Gets the service provider for resolving handlers.
        /// Used by source generators to bypass reflection.
        /// </summary>
        public IServiceProvider? ServiceProvider => _serviceProvider;

        /// <inheritdoc />
        public void SetDispatcher(IDispatcher dispatcher)
        {
            _uiDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        #region Request/Response

        /// <inheritdoc />
        [RequiresDynamicCode("Send constructs handler wrappers via MakeGenericType when handlers are registered through assembly scanning. Use AddModernMediatorGenerated() for the AOT-compatible source-generated registration path.")]
        [RequiresUnreferencedCode("Send uses reflection over handler types when handlers are registered through assembly scanning. Use AddModernMediatorGenerated() for the AOT-compatible source-generated registration path.")]
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (_serviceProvider == null)
                throw new InvalidOperationException(
                    "No service provider configured. Use AddModernMediator() or Mediator.Create(serviceProvider).");

            var requestType = request.GetType();
            var wrapper = (RequestHandlerWrapper<TResponse>)_wrapperCache
                .GetOrAdd(requestType, static t => CreateWrapper(t));

            await wrapper.ExecutePreProcessors(request, _serviceProvider, cancellationToken)
                .ConfigureAwait(false);

            TResponse response;
            try
            {
                response = await wrapper.Handle(request, _serviceProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var handledResult = await TryHandleException<TResponse>(
                    requestType, typeof(TResponse), request, ex, cancellationToken);
                if (handledResult.Handled)
                    return handledResult.Response!;
                throw;
            }

            await wrapper.ExecutePostProcessors(request, response, _serviceProvider, cancellationToken)
                .ConfigureAwait(false);

            return response;
        }

        private static object CreateWrapper(Type requestType)
        {
            var responseType = requestType
                .GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .GetGenericArguments()[0];

            return Activator.CreateInstance(
                typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType))!;
        }

        /// <inheritdoc />
        [RequiresDynamicCode("SendAsync constructs ValueTask handler wrappers via MakeGenericType when handlers are registered through assembly scanning. Use AddModernMediatorGenerated() for the AOT-compatible source-generated registration path.")]
        [RequiresUnreferencedCode("SendAsync uses reflection over handler types when handlers are registered through assembly scanning. Use AddModernMediatorGenerated() for the AOT-compatible source-generated registration path.")]
        public ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (_serviceProvider == null)
                throw new InvalidOperationException(
                    "No service provider configured. Use AddModernMediator() or Mediator.Create(serviceProvider).");

            var requestType = request.GetType();
            var wrapper = (ValueTaskHandlerWrapper<TResponse>)_valueTaskWrapperCache
                .GetOrAdd(requestType, static t => CreateValueTaskWrapper(t));

            return wrapper.Handle(request, _serviceProvider, cancellationToken);
        }

        private static object CreateValueTaskWrapper(Type requestType)
        {
            var responseType = requestType
                .GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .GetGenericArguments()[0];

            return Activator.CreateInstance(
                typeof(ValueTaskHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType))!;
        }

        /// <summary>
        /// Compiled exception handler delegates keyed by interface type. One-time Expression.Lambda compilation per type triple.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Func<object, object, Exception, CancellationToken, Task>>
            _exceptionHandlerDelegateCache = new();

        private async Task<ExceptionHandlingResult<TResponse>> TryHandleException<TResponse>(
            Type requestType,
            Type responseType,
            object request,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (_serviceProvider == null)
            {
                return ExceptionHandlingResult<TResponse>.NotHandled();
            }

            // Try to find exception handlers for this specific exception type
            var exceptionType = exception.GetType();

            // Walk up the exception type hierarchy
            var currentExceptionType = exceptionType;
            while (currentExceptionType != null && currentExceptionType != typeof(object))
            {
                var exceptionHandlerInterfaceType = typeof(IRequestExceptionHandler<,,>)
                    .MakeGenericType(requestType, responseType, currentExceptionType);
                var handlersEnumerableType = typeof(IEnumerable<>).MakeGenericType(exceptionHandlerInterfaceType);
                var handlersObj = _serviceProvider.GetService(handlersEnumerableType);

                if (handlersObj != null)
                {
                    var handlers = ((System.Collections.IEnumerable)handlersObj).Cast<object>().ToList();
                    var invoker = _exceptionHandlerDelegateCache.GetOrAdd(
                        exceptionHandlerInterfaceType,
                        static interfaceType => CompileExceptionHandlerDelegate(interfaceType));

                    foreach (var handler in handlers)
                    {
                        var resultTask = invoker(handler, request, exception, cancellationToken);
                        if (resultTask is Task<ExceptionHandlingResult<TResponse>> typedTask)
                        {
                            var result = await typedTask.ConfigureAwait(false);
                            if (result.Handled)
                            {
                                return result;
                            }
                        }
                    }
                }

                currentExceptionType = currentExceptionType.BaseType;
            }

            return ExceptionHandlingResult<TResponse>.NotHandled();
        }

        private static Func<object, object, Exception, CancellationToken, Task> CompileExceptionHandlerDelegate(Type interfaceType)
        {
            // interfaceType is IRequestExceptionHandler<TRequest, TResponse, TException>
            var genericArgs = interfaceType.GetGenericArguments();
            var requestType = genericArgs[0];
            var exceptionType = genericArgs[2];

            var handleMethod = interfaceType.GetMethod("Handle")!;

            // Parameters: (object handler, object request, Exception exception, CancellationToken ct)
            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var exceptionParam = Expression.Parameter(typeof(Exception), "exception");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

            // ((IRequestExceptionHandler<TReq, TRes, TEx>)handler).Handle((TReq)request, (TEx)exception, ct)
            var call = Expression.Call(
                Expression.Convert(handlerParam, interfaceType),
                handleMethod,
                Expression.Convert(requestParam, requestType),
                Expression.Convert(exceptionParam, exceptionType),
                ctParam);

            // Return type is Task<ExceptionHandlingResult<TResponse>> which is assignable to Task
            var lambda = Expression.Lambda<Func<object, object, Exception, CancellationToken, Task>>(
                call, handlerParam, requestParam, exceptionParam, ctParam);

            return lambda.Compile();
        }

        #endregion

        #region Notifications (DI-based)

        /// <inheritdoc />
        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (_serviceProvider == null)
                return;

            var handlers = (IEnumerable<INotificationHandler<TNotification>>?)
                _serviceProvider.GetService(typeof(IEnumerable<INotificationHandler<TNotification>>));

            if (handlers == null)
                return;

            var handlerList = handlers.ToList();
            if (handlerList.Count == 0)
                return;

            var exceptions = new List<Exception>();

            foreach (var handler in handlerList)
            {
                try
                {
                    await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    HandleError(ex, notification, typeof(TNotification), exceptions, handler.GetType(), handler);
                }
            }

            if (_errorPolicy == ErrorPolicy.ContinueAndAggregate && exceptions.Count > 0)
            {
                throw new AggregateException("One or more handlers threw exceptions", exceptions);
            }
        }

        #endregion

        #region Streaming

        /// <inheritdoc />
        [RequiresDynamicCode("CreateStream constructs stream handler wrappers via MakeGenericType when handlers are registered through assembly scanning. Use AddModernMediatorGenerated() for the AOT-compatible source-generated registration path.")]
        [RequiresUnreferencedCode("CreateStream uses reflection over handler types when handlers are registered through assembly scanning. Use AddModernMediatorGenerated() for the AOT-compatible source-generated registration path.")]
        public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();

            if (_serviceProvider == null)
                throw new InvalidOperationException(
                    "No service provider configured. Use AddModernMediator() or Mediator.Create(serviceProvider).");

            var requestType = request.GetType();
            var wrapper = (StreamHandlerWrapper<TResponse>)_streamWrapperCache
                .GetOrAdd(requestType, static t => CreateStreamWrapper(t));

            var stream = wrapper.Handle(request, _serviceProvider, cancellationToken);

            await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }

        private static object CreateStreamWrapper(Type requestType)
        {
            var responseType = requestType
                .GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>))
                .GetGenericArguments()[0];

            return Activator.CreateInstance(
                typeof(StreamHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType))!;
        }

        #endregion

        #region Subscribe Methods

        /// <inheritdoc />
        public IDisposable Subscribe<T>(Action<T> handler, bool weak = true, Predicate<T>? filter = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ThrowIfDisposed();

            var entry = CreateEntry(handler, weak, filter);
            var messageType = typeof(T);

            _typeHandlersLock.EnterWriteLock();
            try
            {
                var handlers = _typeHandlers.TryGetValue(messageType, out var existing)
                    ? existing.Add(entry)
                    : ImmutableArray.Create(entry);

                _typeHandlers = _typeHandlers.SetItem(messageType, handlers);
                Interlocked.Increment(ref _subscriptionVersion);
            }
            finally
            {
                _typeHandlersLock.ExitWriteLock();
            }

            return new SubscriptionToken(() => Unsubscribe(messageType, entry));
        }

        /// <inheritdoc />
        public IDisposable SubscribeAsync<T>(Func<T, Task> handler, bool weak = true, Predicate<T>? filter = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ThrowIfDisposed();

            var entry = CreateAsyncEntry(handler, weak, filter);
            var messageType = typeof(T);

            _asyncHandlersLock.EnterWriteLock();
            try
            {
                var handlers = _asyncHandlers.TryGetValue(messageType, out var existing)
                    ? existing.Add(entry)
                    : ImmutableArray.Create(entry);

                _asyncHandlers = _asyncHandlers.SetItem(messageType, handlers);
                Interlocked.Increment(ref _subscriptionVersion);
            }
            finally
            {
                _asyncHandlersLock.ExitWriteLock();
            }

            return new SubscriptionToken(() => UnsubscribeAsync(messageType, entry));
        }

        /// <inheritdoc />
        public IDisposable SubscribeOnMainThread<T>(Action<T> handler, bool weak = true, Predicate<T>? filter = null)
        {
            if (_uiDispatcher == null)
                throw new InvalidOperationException("UI dispatcher not set. Call SetDispatcher first.");

            Action<T> wrappedHandler = msg =>
            {
                if (_uiDispatcher.CheckAccess())
                {
                    handler(msg);
                }
                else
                {
                    _uiDispatcher.Invoke(() => handler(msg));
                }
            };

            // CRITICAL: The wrapper closure must use a strong reference (weak: false).
            // If weak: true, the closure can be GC'd immediately since nothing else holds it.
            return Subscribe(wrappedHandler, weak: false, filter);
        }

        /// <inheritdoc />
        public IDisposable SubscribeAsyncOnMainThread<T>(Func<T, Task> handler, bool weak = true, Predicate<T>? filter = null)
        {
            if (_uiDispatcher == null)
                throw new InvalidOperationException("UI dispatcher not set. Call SetDispatcher first.");

            Func<T, Task> wrappedHandler = async msg =>
            {
                if (_uiDispatcher.CheckAccess())
                {
                    await handler(msg);
                }
                else
                {
                    await _uiDispatcher.InvokeAsync(() => handler(msg));
                }
            };

            // CRITICAL: The wrapper closure must use a strong reference (weak: false).
            return SubscribeAsync(wrappedHandler, weak: false, filter);
        }

        /// <inheritdoc />
        public IDisposable Subscribe<T>(string key, Action<T> handler, bool weak = true, Predicate<T>? filter = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ThrowIfDisposed();

            var entry = CreateEntry(handler, weak, filter);
            var messageType = typeof(T);

            _stringHandlersLock.EnterWriteLock();
            try
            {
                if (_stringHandlers.TryGetValue(key, out var existing))
                {
                    if (existing.MessageType != messageType)
                    {
                        throw new ArgumentException(
                            $"Key '{key}' already registered for type {existing.MessageType.Name}, cannot register for {messageType.Name}");
                    }

                    var updatedHandlers = existing.Handlers.Add(entry);
                    _stringHandlers = _stringHandlers.SetItem(key, (messageType, updatedHandlers));
                }
                else
                {
                    _stringHandlers = _stringHandlers.Add(key, (messageType, ImmutableArray.Create(entry)));
                }
            }
            finally
            {
                _stringHandlersLock.ExitWriteLock();
            }

            return new SubscriptionToken(() => UnsubscribeString(key, entry));
        }

        #endregion

        #region Unsubscribe Methods

        private void Unsubscribe(Type messageType, IHandlerEntry entry)
        {
            _typeHandlersLock.EnterWriteLock();
            try
            {
                if (_typeHandlers.TryGetValue(messageType, out var handlers))
                {
                    var updated = handlers.Remove(entry);
                    if (updated.IsEmpty)
                    {
                        _typeHandlers = _typeHandlers.Remove(messageType);
                    }
                    else
                    {
                        _typeHandlers = _typeHandlers.SetItem(messageType, updated);
                    }

                    Interlocked.Increment(ref _subscriptionVersion);
                }
            }
            finally
            {
                _typeHandlersLock.ExitWriteLock();
            }
        }

        private void UnsubscribeAsync(Type messageType, IAsyncHandlerEntry entry)
        {
            _asyncHandlersLock.EnterWriteLock();
            try
            {
                if (_asyncHandlers.TryGetValue(messageType, out var handlers))
                {
                    var updated = handlers.Remove(entry);
                    if (updated.IsEmpty)
                    {
                        _asyncHandlers = _asyncHandlers.Remove(messageType);
                    }
                    else
                    {
                        _asyncHandlers = _asyncHandlers.SetItem(messageType, updated);
                    }
                    Interlocked.Increment(ref _subscriptionVersion);
                }
            }
            finally
            {
                _asyncHandlersLock.ExitWriteLock();
            }
        }

        private void UnsubscribeString(string key, IHandlerEntry entry)
        {
            _stringHandlersLock.EnterWriteLock();
            try
            {
                if (_stringHandlers.TryGetValue(key, out var existing))
                {
                    var updated = existing.Handlers.Remove(entry);
                    if (updated.IsEmpty)
                    {
                        _stringHandlers = _stringHandlers.Remove(key);
                    }
                    else
                    {
                        _stringHandlers = _stringHandlers.SetItem(key, (existing.MessageType, updated));
                    }
                }
            }
            finally
            {
                _stringHandlersLock.ExitWriteLock();
            }
        }

        #endregion

        #region Publish Methods

        /// <inheritdoc />
        public bool Publish<T>(T? message)
        {
            ThrowIfDisposed();

            if (message == null) return false;

            var messageType = typeof(T);
            var (handlers, matchedTypes) = GetHandlersForTypeWithCache(messageType);

            if (handlers.IsEmpty)
                return false;

            bool anyInvoked = InvokeHandlers(handlers, message, messageType);
            PruneDeadHandlers(matchedTypes);

            return anyInvoked;
        }

        /// <inheritdoc />
        public bool Publish<T>(string key, T? message)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            if (message == null) return false;

            var handlers = GetHandlersForKey(key);

            if (handlers.IsEmpty)
                return false;

            bool anyInvoked = InvokeHandlers(handlers, message, typeof(T));
            PruneDeadHandlersForKey(key);

            return anyInvoked;
        }

        /// <inheritdoc />
        public Task<bool> PublishAsync<T>(T? message, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Publish(message);
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<bool> PublishAsync<T>(string key, T? message, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Publish(key, message);
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> PublishAsyncTrue<T>(T? message, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (message == null) return false;

            cancellationToken.ThrowIfCancellationRequested();

            var messageType = typeof(T);
            var (handlers, matchedTypes) = GetAsyncHandlersForType(messageType);

            if (handlers.IsEmpty)
                return false;

            var trackedTasks = new List<TrackedHandlerTask<IAsyncHandlerEntry>>();
            var exceptions = new List<Exception>();

            foreach (var h in handlers)
            {
                try
                {
                    if (!h.IsAlive) continue;

                    var task = h.TryInvokeAsync(message);
                    if (task != null)
                    {
                        trackedTasks.Add(new TrackedHandlerTask<IAsyncHandlerEntry>(task, h));
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ex, message, messageType, exceptions, h.HandlerType, h.HandlerInstance);
                }
            }

            if (trackedTasks.Count > 0)
            {
                var whenAllTask = Task.WhenAll(trackedTasks.Select(t => t.Task));

                try
                {
                    await whenAllTask.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Per ADR-005, attribute each per-handler fault individually so HandlerError
                    // fires once per failing handler with full HandlerType/HandlerInstance, rather
                    // than once with the aggregate and null attribution.
                    foreach (var tracked in trackedTasks)
                    {
                        if (!tracked.Task.IsFaulted) continue;

                        var taskException = tracked.Task.Exception;
                        if (taskException == null) continue;

                        foreach (var innerEx in taskException.Flatten().InnerExceptions)
                        {
                            HandleError(innerEx, message, messageType, exceptions, tracked.Entry.HandlerType, tracked.Entry.HandlerInstance);
                        }
                    }
                }
            }

            PruneDeadAsyncHandlers(matchedTypes);

            if (exceptions.Count > 0 && _errorPolicy == ErrorPolicy.ContinueAndAggregate)
            {
                throw new AggregateException("One or more async handlers threw exceptions", exceptions);
            }

            return trackedTasks.Count > 0;
        }

        #endregion

        #region Pub/Sub with Callbacks

        /// <inheritdoc />
        public IDisposable Subscribe<TMessage, TResponse>(Func<TMessage, TResponse> handler, bool weak = true, Predicate<TMessage>? filter = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ThrowIfDisposed();

            var messageType = typeof(TMessage);
            var entry = CreateCallbackEntry(handler, weak, filter);

            _callbackHandlersLock.EnterWriteLock();
            try
            {
                var currentHandlers = _callbackHandlers.TryGetValue(messageType, out var h)
                    ? h
                    : ImmutableArray<ICallbackHandlerEntry>.Empty;

                _callbackHandlers = _callbackHandlers.SetItem(messageType, currentHandlers.Add(entry));
                Interlocked.Increment(ref _subscriptionVersion);
            }
            finally
            {
                _callbackHandlersLock.ExitWriteLock();
            }

            return new SubscriptionToken(() => UnsubscribeCallback(messageType, entry));
        }

        /// <inheritdoc />
        public IDisposable SubscribeAsync<TMessage, TResponse>(Func<TMessage, Task<TResponse>> handler, bool weak = true, Predicate<TMessage>? filter = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ThrowIfDisposed();

            var messageType = typeof(TMessage);
            var entry = CreateAsyncCallbackEntry(handler, weak, filter);

            _asyncCallbackHandlersLock.EnterWriteLock();
            try
            {
                var currentHandlers = _asyncCallbackHandlers.TryGetValue(messageType, out var h)
                    ? h
                    : ImmutableArray<IAsyncCallbackHandlerEntry>.Empty;

                _asyncCallbackHandlers = _asyncCallbackHandlers.SetItem(messageType, currentHandlers.Add(entry));
                Interlocked.Increment(ref _subscriptionVersion);
            }
            finally
            {
                _asyncCallbackHandlersLock.ExitWriteLock();
            }

            return new SubscriptionToken(() => UnsubscribeAsyncCallback(messageType, entry));
        }

        /// <inheritdoc />
        public IReadOnlyList<TResponse> Publish<TMessage, TResponse>(TMessage? message)
        {
            ThrowIfDisposed();

            if (message == null) return Array.Empty<TResponse>();

            var messageType = typeof(TMessage);
            var handlers = GetCallbackHandlersForType(messageType);

            if (handlers.IsEmpty)
                return Array.Empty<TResponse>();

            var responses = new List<TResponse>();
            var exceptions = new List<Exception>();

            foreach (var h in handlers)
            {
                try
                {
                    if (!h.IsAlive) continue;

                    if (h.TryInvoke(message, out var response) && response is TResponse typedResponse)
                    {
                        responses.Add(typedResponse);
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ex, message, messageType, exceptions, h.HandlerType, h.HandlerInstance);
                }
            }

            PruneDeadCallbackHandlers(messageType);

            if (exceptions.Count > 0 && _errorPolicy == ErrorPolicy.ContinueAndAggregate)
            {
                throw new AggregateException("One or more callback handlers threw exceptions", exceptions);
            }

            return responses;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TResponse>> PublishAsync<TMessage, TResponse>(TMessage? message, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (message == null) return Array.Empty<TResponse>();

            cancellationToken.ThrowIfCancellationRequested();

            var messageType = typeof(TMessage);
            var handlers = GetAsyncCallbackHandlersForType(messageType);

            if (handlers.IsEmpty)
                return Array.Empty<TResponse>();

            var trackedTasks = new List<TrackedHandlerTask<IAsyncCallbackHandlerEntry>>();
            var exceptions = new List<Exception>();

            foreach (var h in handlers)
            {
                try
                {
                    if (!h.IsAlive) continue;

                    var task = h.TryInvokeAsync(message);
                    if (task != null)
                    {
                        trackedTasks.Add(new TrackedHandlerTask<IAsyncCallbackHandlerEntry>(task, h));
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ex, message, messageType, exceptions, h.HandlerType, h.HandlerInstance);
                }
            }

            var responses = new List<TResponse>();

            if (trackedTasks.Count > 0)
            {
                var whenAllTask = Task.WhenAll(trackedTasks.Select(t => (Task<object?>)t.Task));

                try
                {
                    var results = await whenAllTask.WaitAsync(cancellationToken);
                    foreach (var result in results)
                    {
                        if (result is TResponse typedResponse)
                        {
                            responses.Add(typedResponse);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    foreach (var tracked in trackedTasks)
                    {
                        if (!tracked.Task.IsFaulted) continue;

                        var taskException = tracked.Task.Exception;
                        if (taskException == null) continue;

                        foreach (var innerEx in taskException.Flatten().InnerExceptions)
                        {
                            HandleError(innerEx, message, messageType, exceptions, tracked.Entry.HandlerType, tracked.Entry.HandlerInstance);
                        }
                    }
                }
            }

            PruneDeadAsyncCallbackHandlers(messageType);

            if (exceptions.Count > 0 && _errorPolicy == ErrorPolicy.ContinueAndAggregate)
            {
                throw new AggregateException("One or more async callback handlers threw exceptions", exceptions);
            }

            return responses;
        }

        private void UnsubscribeCallback(Type messageType, ICallbackHandlerEntry entry)
        {
            _callbackHandlersLock.EnterWriteLock();
            try
            {
                if (_callbackHandlers.TryGetValue(messageType, out var handlers))
                {
                    var newHandlers = handlers.Remove(entry);
                    if (newHandlers.IsEmpty)
                        _callbackHandlers = _callbackHandlers.Remove(messageType);
                    else
                        _callbackHandlers = _callbackHandlers.SetItem(messageType, newHandlers);

                    Interlocked.Increment(ref _subscriptionVersion);
                }
            }
            finally
            {
                _callbackHandlersLock.ExitWriteLock();
            }
        }

        private void UnsubscribeAsyncCallback(Type messageType, IAsyncCallbackHandlerEntry entry)
        {
            _asyncCallbackHandlersLock.EnterWriteLock();
            try
            {
                if (_asyncCallbackHandlers.TryGetValue(messageType, out var handlers))
                {
                    var newHandlers = handlers.Remove(entry);
                    if (newHandlers.IsEmpty)
                        _asyncCallbackHandlers = _asyncCallbackHandlers.Remove(messageType);
                    else
                        _asyncCallbackHandlers = _asyncCallbackHandlers.SetItem(messageType, newHandlers);

                    Interlocked.Increment(ref _subscriptionVersion);
                }
            }
            finally
            {
                _asyncCallbackHandlersLock.ExitWriteLock();
            }
        }

        private ImmutableArray<ICallbackHandlerEntry> GetCallbackHandlersForType(Type messageType)
        {
            _callbackHandlersLock.EnterReadLock();
            try
            {
                return _callbackHandlers.TryGetValue(messageType, out var handlers)
                    ? handlers
                    : ImmutableArray<ICallbackHandlerEntry>.Empty;
            }
            finally
            {
                _callbackHandlersLock.ExitReadLock();
            }
        }

        private ImmutableArray<IAsyncCallbackHandlerEntry> GetAsyncCallbackHandlersForType(Type messageType)
        {
            _asyncCallbackHandlersLock.EnterReadLock();
            try
            {
                return _asyncCallbackHandlers.TryGetValue(messageType, out var handlers)
                    ? handlers
                    : ImmutableArray<IAsyncCallbackHandlerEntry>.Empty;
            }
            finally
            {
                _asyncCallbackHandlersLock.ExitReadLock();
            }
        }

        private void PruneDeadCallbackHandlers(Type messageType)
        {
            _callbackHandlersLock.EnterUpgradeableReadLock();
            try
            {
                if (!_callbackHandlers.TryGetValue(messageType, out var handlers))
                    return;

                var aliveBuilder = ImmutableArray.CreateBuilder<ICallbackHandlerEntry>(handlers.Length);
                var hasDeadHandlers = false;

                foreach (var h in handlers)
                {
                    if (h.IsAlive)
                        aliveBuilder.Add(h);
                    else
                        hasDeadHandlers = true;
                }

                if (hasDeadHandlers)
                {
                    _callbackHandlersLock.EnterWriteLock();
                    try
                    {
                        var alive = aliveBuilder.ToImmutable();

                        if (alive.IsEmpty)
                            _callbackHandlers = _callbackHandlers.Remove(messageType);
                        else
                            _callbackHandlers = _callbackHandlers.SetItem(messageType, alive);

                        Interlocked.Increment(ref _subscriptionVersion);
                    }
                    finally
                    {
                        _callbackHandlersLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _callbackHandlersLock.ExitUpgradeableReadLock();
            }
        }

        private void PruneDeadAsyncCallbackHandlers(Type messageType)
        {
            _asyncCallbackHandlersLock.EnterUpgradeableReadLock();
            try
            {
                if (!_asyncCallbackHandlers.TryGetValue(messageType, out var handlers))
                    return;

                var aliveBuilder = ImmutableArray.CreateBuilder<IAsyncCallbackHandlerEntry>(handlers.Length);
                var hasDeadHandlers = false;

                foreach (var h in handlers)
                {
                    if (h.IsAlive)
                        aliveBuilder.Add(h);
                    else
                        hasDeadHandlers = true;
                }

                if (hasDeadHandlers)
                {
                    _asyncCallbackHandlersLock.EnterWriteLock();
                    try
                    {
                        var alive = aliveBuilder.ToImmutable();

                        if (alive.IsEmpty)
                            _asyncCallbackHandlers = _asyncCallbackHandlers.Remove(messageType);
                        else
                            _asyncCallbackHandlers = _asyncCallbackHandlers.SetItem(messageType, alive);

                        Interlocked.Increment(ref _subscriptionVersion);
                    }
                    finally
                    {
                        _asyncCallbackHandlersLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _asyncCallbackHandlersLock.ExitUpgradeableReadLock();
            }
        }

        #endregion

        #region Helper Methods

        private static IHandlerEntry CreateEntry<T>(Action<T> handler, bool weak, Predicate<T>? filter)
        {
            return weak
                ? new WeakHandlerEntry<T>(handler, filter)
                : new StrongHandlerEntry<T>(handler, filter);
        }

        private static IAsyncHandlerEntry CreateAsyncEntry<T>(Func<T, Task> handler, bool weak, Predicate<T>? filter)
        {
            return weak
                ? new WeakAsyncHandlerEntry<T>(handler, filter)
                : new StrongAsyncHandlerEntry<T>(handler, filter);
        }

        private static ICallbackHandlerEntry CreateCallbackEntry<TMessage, TResponse>(Func<TMessage, TResponse> handler, bool weak, Predicate<TMessage>? filter)
        {
            return weak
                ? new WeakCallbackHandlerEntry<TMessage, TResponse>(handler, filter)
                : new StrongCallbackHandlerEntry<TMessage, TResponse>(handler, filter);
        }

        private static IAsyncCallbackHandlerEntry CreateAsyncCallbackEntry<TMessage, TResponse>(Func<TMessage, Task<TResponse>> handler, bool weak, Predicate<TMessage>? filter)
        {
            return weak
                ? new WeakAsyncCallbackHandlerEntry<TMessage, TResponse>(handler, filter)
                : new StrongAsyncCallbackHandlerEntry<TMessage, TResponse>(handler, filter);
        }

        private (ImmutableArray<IHandlerEntry>, IReadOnlyList<Type>) GetHandlersForTypeWithCache(Type messageType)
        {
            var currentVersion = Volatile.Read(ref _subscriptionVersion);
            var cacheKey = (messageType, currentVersion);

            if (_typeMatchCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            _typeHandlersLock.EnterReadLock();
            try
            {
                var matchedTypes = new List<Type>();
                var allHandlers = ImmutableArray.CreateBuilder<IHandlerEntry>();

                if (_typeHandlers.TryGetValue(messageType, out var exactHandlers))
                {
                    matchedTypes.Add(messageType);
                    allHandlers.AddRange(exactHandlers);
                }

                foreach (var kvp in _typeHandlers)
                {
                    if (kvp.Key == messageType) continue;

                    if (kvp.Key.IsAssignableFrom(messageType))
                    {
                        matchedTypes.Add(kvp.Key);
                        allHandlers.AddRange(kvp.Value);
                    }
                }

                var result = (allHandlers.ToImmutable(), (IReadOnlyList<Type>)matchedTypes);

                _typeMatchCache[cacheKey] = result;

                var versionGap = 10;
                if (currentVersion > versionGap)
                {
                    var oldKeys = _typeMatchCache.Keys
                        .Where(k => k.Item2 < currentVersion - versionGap)
                        .ToList();

                    foreach (var oldKey in oldKeys)
                    {
                        _typeMatchCache.TryRemove(oldKey, out _);
                    }
                }

                return result;
            }
            finally
            {
                _typeHandlersLock.ExitReadLock();
            }
        }

        private (ImmutableArray<IAsyncHandlerEntry>, IReadOnlyList<Type>) GetAsyncHandlersForType(Type messageType)
        {
            _asyncHandlersLock.EnterReadLock();
            try
            {
                var matchedTypes = new List<Type>();
                var allHandlers = ImmutableArray.CreateBuilder<IAsyncHandlerEntry>();

                if (_asyncHandlers.TryGetValue(messageType, out var exactHandlers))
                {
                    matchedTypes.Add(messageType);
                    allHandlers.AddRange(exactHandlers);
                }

                foreach (var kvp in _asyncHandlers)
                {
                    if (kvp.Key == messageType) continue;

                    if (kvp.Key.IsAssignableFrom(messageType))
                    {
                        matchedTypes.Add(kvp.Key);
                        allHandlers.AddRange(kvp.Value);
                    }
                }

                return (allHandlers.ToImmutable(), matchedTypes);
            }
            finally
            {
                _asyncHandlersLock.ExitReadLock();
            }
        }

        private ImmutableArray<IHandlerEntry> GetHandlersForKey(string key)
        {
            _stringHandlersLock.EnterReadLock();
            try
            {
                return _stringHandlers.TryGetValue(key, out var entry)
                    ? entry.Handlers
                    : ImmutableArray<IHandlerEntry>.Empty;
            }
            finally
            {
                _stringHandlersLock.ExitReadLock();
            }
        }

        private bool InvokeHandlers(ImmutableArray<IHandlerEntry> handlers, object message, Type messageType)
        {
            var exceptions = new List<Exception>();
            var anyInvoked = false;

            foreach (var h in handlers)
            {
                try
                {
                    if (!h.IsAlive) continue;

                    if (h.TryInvoke(message))
                    {
                        anyInvoked = true;
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ex, message, messageType, exceptions, h.HandlerType, h.HandlerInstance);

                    if (_errorPolicy == ErrorPolicy.StopOnFirstError)
                        break;
                }
            }

            if (exceptions.Count > 0 && _errorPolicy == ErrorPolicy.ContinueAndAggregate)
            {
                throw new AggregateException("One or more handlers threw exceptions", exceptions);
            }

            return anyInvoked;
        }

        private void HandleError(Exception ex, object message, Type messageType, List<Exception> exceptions, Type? handlerType = null, object? handlerInstance = null)
        {
            // Unwrap TargetInvocationException from reflection-based invocation (weak references)
            // so users get the original exception, not the reflection wrapper
            var actualException = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                ? tie.InnerException
                : ex;

            var errorArgs = new HandlerErrorEventArgs(actualException, message, messageType, handlerType, handlerInstance);

            var handler = HandlerError;
            if (handler != null)
            {
                foreach (var subscriber in handler.GetInvocationList())
                {
                    try
                    {
                        ((EventHandler<HandlerErrorEventArgs>)subscriber).Invoke(this, errorArgs);
                    }
                    catch (Exception subscriberEx)
                    {
                        GetSubscriberExceptionSink().OnSubscriberException(subscriberEx);
                    }
                }
            }

            if (_errorPolicy == ErrorPolicy.ContinueAndAggregate)
            {
                exceptions.Add(actualException);
            }
            else if (_errorPolicy == ErrorPolicy.StopOnFirstError)
            {
                throw actualException;
            }
        }

        private ISubscriberExceptionSink GetSubscriberExceptionSink()
        {
            if (!_sinkResolved)
            {
                var registered = _serviceProvider?.GetService(typeof(ISubscriberExceptionSink)) as ISubscriberExceptionSink;
                if (registered != null)
                {
                    _subscriberExceptionSink = registered;
                }
                else
                {
                    var logger = _serviceProvider?.GetService(typeof(Microsoft.Extensions.Logging.ILogger<Mediator>)) as Microsoft.Extensions.Logging.ILogger<Mediator>;
                    _subscriberExceptionSink = new LoggerSubscriberExceptionSink(logger);
                }
                _sinkResolved = true;
            }
            return _subscriberExceptionSink!;
        }

        private void PruneDeadHandlers(IReadOnlyList<Type> matchedTypes)
        {
            foreach (var messageType in matchedTypes)
            {
                PruneDeadHandlersForType(messageType);
            }
        }

        private void PruneDeadAsyncHandlers(IReadOnlyList<Type> matchedTypes)
        {
            foreach (var messageType in matchedTypes)
            {
                PruneDeadAsyncHandlersForType(messageType);
            }
        }

        private void PruneDeadHandlersForType(Type messageType)
        {
            _typeHandlersLock.EnterUpgradeableReadLock();
            try
            {
                if (!_typeHandlers.TryGetValue(messageType, out var handlers))
                    return;

                var aliveBuilder = ImmutableArray.CreateBuilder<IHandlerEntry>(handlers.Length);
                var hasDeadHandlers = false;

                foreach (var h in handlers)
                {
                    if (h.IsAlive)
                    {
                        aliveBuilder.Add(h);
                    }
                    else
                    {
                        hasDeadHandlers = true;
                    }
                }

                if (hasDeadHandlers)
                {
                    _typeHandlersLock.EnterWriteLock();
                    try
                    {
                        var alive = aliveBuilder.ToImmutable();

                        if (alive.IsEmpty)
                        {
                            _typeHandlers = _typeHandlers.Remove(messageType);
                        }
                        else
                        {
                            _typeHandlers = _typeHandlers.SetItem(messageType, alive);
                        }

                        Interlocked.Increment(ref _subscriptionVersion);
                    }
                    finally
                    {
                        _typeHandlersLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _typeHandlersLock.ExitUpgradeableReadLock();
            }
        }

        private void PruneDeadAsyncHandlersForType(Type messageType)
        {
            _asyncHandlersLock.EnterUpgradeableReadLock();
            try
            {
                if (!_asyncHandlers.TryGetValue(messageType, out var handlers))
                    return;

                var aliveBuilder = ImmutableArray.CreateBuilder<IAsyncHandlerEntry>(handlers.Length);
                var hasDeadHandlers = false;

                foreach (var h in handlers)
                {
                    if (h.IsAlive)
                        aliveBuilder.Add(h);
                    else
                        hasDeadHandlers = true;
                }

                if (hasDeadHandlers)
                {
                    _asyncHandlersLock.EnterWriteLock();
                    try
                    {
                        var alive = aliveBuilder.ToImmutable();

                        if (alive.IsEmpty)
                            _asyncHandlers = _asyncHandlers.Remove(messageType);
                        else
                            _asyncHandlers = _asyncHandlers.SetItem(messageType, alive);

                        Interlocked.Increment(ref _subscriptionVersion);
                    }
                    finally
                    {
                        _asyncHandlersLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _asyncHandlersLock.ExitUpgradeableReadLock();
            }
        }

        private void PruneDeadHandlersForKey(string key)
        {
            _stringHandlersLock.EnterUpgradeableReadLock();
            try
            {
                if (!_stringHandlers.TryGetValue(key, out var entry))
                    return;

                var aliveBuilder = ImmutableArray.CreateBuilder<IHandlerEntry>(entry.Handlers.Length);
                var hasDeadHandlers = false;

                foreach (var h in entry.Handlers)
                {
                    if (h.IsAlive)
                    {
                        aliveBuilder.Add(h);
                    }
                    else
                    {
                        hasDeadHandlers = true;
                    }
                }

                if (hasDeadHandlers)
                {
                    _stringHandlersLock.EnterWriteLock();
                    try
                    {
                        var alive = aliveBuilder.ToImmutable();

                        if (alive.IsEmpty)
                        {
                            _stringHandlers = _stringHandlers.Remove(key);
                        }
                        else
                        {
                            _stringHandlers = _stringHandlers.SetItem(key, (entry.MessageType, alive));
                        }
                    }
                    finally
                    {
                        _stringHandlersLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _stringHandlersLock.ExitUpgradeableReadLock();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Mediator));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the Mediator and clears all subscriptions.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _typeHandlersLock.EnterWriteLock();
            try
            {
                _typeHandlers = ImmutableDictionary<Type, ImmutableArray<IHandlerEntry>>.Empty;
            }
            finally
            {
                _typeHandlersLock.ExitWriteLock();
            }

            _asyncHandlersLock.EnterWriteLock();
            try
            {
                _asyncHandlers = ImmutableDictionary<Type, ImmutableArray<IAsyncHandlerEntry>>.Empty;
            }
            finally
            {
                _asyncHandlersLock.ExitWriteLock();
            }

            _callbackHandlersLock.EnterWriteLock();
            try
            {
                _callbackHandlers = ImmutableDictionary<Type, ImmutableArray<ICallbackHandlerEntry>>.Empty;
            }
            finally
            {
                _callbackHandlersLock.ExitWriteLock();
            }

            _asyncCallbackHandlersLock.EnterWriteLock();
            try
            {
                _asyncCallbackHandlers = ImmutableDictionary<Type, ImmutableArray<IAsyncCallbackHandlerEntry>>.Empty;
            }
            finally
            {
                _asyncCallbackHandlersLock.ExitWriteLock();
            }

            _stringHandlersLock.EnterWriteLock();
            try
            {
                _stringHandlers = ImmutableDictionary<string, (Type, ImmutableArray<IHandlerEntry>)>.Empty;
            }
            finally
            {
                _stringHandlersLock.ExitWriteLock();
            }

            _typeMatchCache.Clear();

            _typeHandlersLock.Dispose();
            _asyncHandlersLock.Dispose();
            _callbackHandlersLock.Dispose();
            _asyncCallbackHandlersLock.Dispose();
            _stringHandlersLock.Dispose();
        }

        #endregion
    }
}