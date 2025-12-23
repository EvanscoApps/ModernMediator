using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    public sealed class Mediator : IMediator
    {
        private static readonly Lazy<IMediator> _instance = new Lazy<IMediator>(() => new Mediator());

        // Request/Response handlers (DI-registered)
        private IServiceProvider? _serviceProvider;

        // Pub/Sub handlers
        private readonly ReaderWriterLockSlim _typeHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<Type, ImmutableArray<IHandlerEntry>> _typeHandlers =
            ImmutableDictionary<Type, ImmutableArray<IHandlerEntry>>.Empty;

        private readonly ReaderWriterLockSlim _asyncHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<Type, ImmutableArray<IAsyncHandlerEntry>> _asyncHandlers =
            ImmutableDictionary<Type, ImmutableArray<IAsyncHandlerEntry>>.Empty;

        private readonly ReaderWriterLockSlim _stringHandlersLock = new ReaderWriterLockSlim();
        private ImmutableDictionary<string, (Type MessageType, ImmutableArray<IHandlerEntry> Handlers)> _stringHandlers =
            ImmutableDictionary<string, (Type, ImmutableArray<IHandlerEntry>)>.Empty;

        private int _subscriptionVersion;
        private readonly ConcurrentDictionary<(Type, int), (ImmutableArray<IHandlerEntry>, IReadOnlyList<Type>)> _typeMatchCache =
            new ConcurrentDictionary<(Type, int), (ImmutableArray<IHandlerEntry>, IReadOnlyList<Type>)>();

        private IDispatcher? _uiDispatcher;
        private ErrorPolicy _errorPolicy = ErrorPolicy.ContinueAndAggregate;
        private bool _disposed;

        /// <summary>
        /// Gets the singleton instance of the Mediator.
        /// For new applications, prefer dependency injection via AddModernMediator().
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
        /// Creates a new Mediator instance.
        /// Use Mediator.Create(), Mediator.Instance, or AddModernMediator() for DI instead.
        /// </summary>
        internal Mediator() { }

        /// <summary>
        /// Sets the service provider for resolving request handlers.
        /// Called internally by DI registration.
        /// </summary>
        internal void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public void SetDispatcher(IDispatcher dispatcher)
        {
            _uiDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        #region Request/Response

        /// <inheritdoc />
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            var requestType = request.GetType();
            var responseType = typeof(TResponse);

            // Get the handler
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            object? handler = _serviceProvider?.GetService(handlerType);

            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"No handler registered for request type {requestType.Name}. " +
                    $"Register a handler implementing IRequestHandler<{requestType.Name}, {responseType.Name}> " +
                    "using AddModernMediator() with assembly scanning or manual registration.");
            }

            // Build the pipeline
            var pipeline = BuildPipeline<TResponse>(requestType, responseType, request, handler, cancellationToken);

            // Execute pre-processors
            await ExecutePreProcessors(requestType, request, cancellationToken);

            // Execute the pipeline (behaviors + handler)
            TResponse response;
            try
            {
                response = await pipeline();
            }
            catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }

            // Execute post-processors
            await ExecutePostProcessors(requestType, responseType, request, response, cancellationToken);

            return response;
        }

        private RequestHandlerDelegate<TResponse> BuildPipeline<TResponse>(
            Type requestType,
            Type responseType,
            object request,
            object handler,
            CancellationToken cancellationToken)
        {
            // Get the Handle method on the handler
            var handleMethod = handler.GetType().GetMethod("Handle");
            if (handleMethod == null)
            {
                throw new InvalidOperationException($"Handle method not found on handler type {handler.GetType().Name}");
            }

            // Create the innermost delegate that calls the actual handler
            RequestHandlerDelegate<TResponse> handlerDelegate = async () =>
            {
                try
                {
                    var resultTask = (Task<TResponse>?)handleMethod.Invoke(handler, new object[] { request, cancellationToken });
                    if (resultTask == null)
                    {
                        throw new InvalidOperationException($"Handler for {requestType.Name} returned null task.");
                    }
                    return await resultTask.ConfigureAwait(false);
                }
                catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
                {
                    throw tie.InnerException;
                }
            };

            // Get all pipeline behaviors
            if (_serviceProvider == null)
            {
                return handlerDelegate;
            }

            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            var behaviorsEnumerableType = typeof(IEnumerable<>).MakeGenericType(behaviorType);
            var behaviorsObj = _serviceProvider.GetService(behaviorsEnumerableType);

            if (behaviorsObj == null)
            {
                return handlerDelegate;
            }

            var behaviors = ((System.Collections.IEnumerable)behaviorsObj).Cast<object>().Reverse().ToList();

            if (behaviors.Count == 0)
            {
                return handlerDelegate;
            }

            // Wrap behaviors around the handler, innermost first (so outermost executes first)
            var currentDelegate = handlerDelegate;

            foreach (var behavior in behaviors)
            {
                var behaviorHandleMethod = behavior.GetType().GetMethod("Handle");
                if (behaviorHandleMethod == null) continue;

                var capturedDelegate = currentDelegate;
                var capturedBehavior = behavior;

                currentDelegate = async () =>
                {
                    try
                    {
                        var resultTask = (Task<TResponse>?)behaviorHandleMethod.Invoke(
                            capturedBehavior,
                            new object[] { request, capturedDelegate, cancellationToken });

                        if (resultTask == null)
                        {
                            throw new InvalidOperationException($"Behavior {capturedBehavior.GetType().Name} returned null task.");
                        }

                        return await resultTask.ConfigureAwait(false);
                    }
                    catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
                    {
                        throw tie.InnerException;
                    }
                };
            }

            return currentDelegate;
        }

        private async Task ExecutePreProcessors(Type requestType, object request, CancellationToken cancellationToken)
        {
            if (_serviceProvider == null) return;

            var preProcessorType = typeof(IRequestPreProcessor<>).MakeGenericType(requestType);
            var preProcessorsEnumerableType = typeof(IEnumerable<>).MakeGenericType(preProcessorType);
            var preProcessorsObj = _serviceProvider.GetService(preProcessorsEnumerableType);

            if (preProcessorsObj == null) return;

            var preProcessors = ((System.Collections.IEnumerable)preProcessorsObj).Cast<object>().ToList();

            foreach (var preProcessor in preProcessors)
            {
                var processMethod = preProcessor.GetType().GetMethod("Process");
                if (processMethod == null) continue;

                try
                {
                    var task = (Task?)processMethod.Invoke(preProcessor, new object[] { request, cancellationToken });
                    if (task != null)
                    {
                        await task.ConfigureAwait(false);
                    }
                }
                catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
                {
                    throw tie.InnerException;
                }
            }
        }

        private async Task ExecutePostProcessors<TResponse>(
            Type requestType,
            Type responseType,
            object request,
            TResponse response,
            CancellationToken cancellationToken)
        {
            if (_serviceProvider == null) return;

            var postProcessorType = typeof(IRequestPostProcessor<,>).MakeGenericType(requestType, responseType);
            var postProcessorsEnumerableType = typeof(IEnumerable<>).MakeGenericType(postProcessorType);
            var postProcessorsObj = _serviceProvider.GetService(postProcessorsEnumerableType);

            if (postProcessorsObj == null) return;

            var postProcessors = ((System.Collections.IEnumerable)postProcessorsObj).Cast<object>().ToList();

            foreach (var postProcessor in postProcessors)
            {
                var processMethod = postProcessor.GetType().GetMethod("Process");
                if (processMethod == null) continue;

                try
                {
                    var task = (Task?)processMethod.Invoke(postProcessor, new object[] { request, response!, cancellationToken });
                    if (task != null)
                    {
                        await task.ConfigureAwait(false);
                    }
                }
                catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
                {
                    throw tie.InnerException;
                }
            }
        }

        #endregion

        #region Streaming

        /// <inheritdoc />
        public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();

            var requestType = request.GetType();
            var responseType = typeof(TResponse);

            // Get the handler
            var handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);
            object? handler = _serviceProvider?.GetService(handlerType);

            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"No stream handler registered for request type {requestType.Name}. " +
                    $"Register a handler implementing IStreamRequestHandler<{requestType.Name}, {responseType.Name}> " +
                    "using AddModernMediator() with assembly scanning or manual registration.");
            }

            // Get the Handle method
            var handleMethod = handlerType.GetMethod("Handle");
            if (handleMethod == null)
            {
                throw new InvalidOperationException($"Handle method not found on handler type {handlerType.Name}");
            }

            // Invoke the handler to get the IAsyncEnumerable
            object? streamResult;
            try
            {
                streamResult = handleMethod.Invoke(handler, new object[] { request, cancellationToken });
            }
            catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }

            if (streamResult == null)
            {
                throw new InvalidOperationException($"Stream handler for {requestType.Name} returned null.");
            }

            var asyncEnumerable = (IAsyncEnumerable<TResponse>)streamResult;

            // Yield items from the stream
            await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
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

            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            foreach (var h in handlers)
            {
                try
                {
                    if (!h.IsAlive) continue;

                    var task = h.TryInvokeAsync(message);
                    if (task != null)
                    {
                        tasks.Add(task);
                    }
                }
                catch (Exception ex)
                {
                    // Unwrap TargetInvocationException from reflection-based invocation
                    var actualException = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                        ? tie.InnerException
                        : ex;

                    var errorArgs = new HandlerErrorEventArgs(actualException, message, messageType);
                    var handler = HandlerError;
                    handler?.Invoke(this, errorArgs);

                    if (_errorPolicy == ErrorPolicy.ContinueAndAggregate)
                    {
                        exceptions.Add(actualException);
                    }
                    else if (_errorPolicy == ErrorPolicy.StopOnFirstError)
                    {
                        throw actualException;
                    }
                }
            }

            if (tasks.Count > 0)
            {
                // CRITICAL FIX: Store the Task.WhenAll task so we can access its .Exception property.
                // When you 'await' a faulted Task.WhenAll, C# only throws the FIRST exception.
                // The full AggregateException with ALL exceptions is only available via task.Exception.
                var whenAllTask = Task.WhenAll(tasks);

                try
                {
                    await whenAllTask.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Cancellation was requested - re-throw to propagate cancellation
                    throw;
                }
                catch
                {
                    // The 'await' unwraps the AggregateException and only throws the first inner exception.
                    // Access whenAllTask.Exception to get the complete AggregateException with ALL failures.
                    var aggregateException = whenAllTask.Exception;

                    if (aggregateException != null)
                    {
                        var errorArgs = new HandlerErrorEventArgs(aggregateException, message, messageType);
                        var handler = HandlerError;
                        handler?.Invoke(this, errorArgs);

                        if (_errorPolicy == ErrorPolicy.ContinueAndAggregate)
                        {
                            var flattened = aggregateException.Flatten();
                            // Unwrap TargetInvocationException from reflection-based invocation
                            foreach (var innerEx in flattened.InnerExceptions)
                            {
                                var actualException = innerEx is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                                    ? tie.InnerException
                                    : innerEx;
                                exceptions.Add(actualException);
                            }
                        }
                        else if (_errorPolicy == ErrorPolicy.StopOnFirstError)
                        {
                            throw;
                        }
                    }
                }
            }

            PruneDeadAsyncHandlers(matchedTypes);

            if (exceptions.Count > 0 && _errorPolicy == ErrorPolicy.ContinueAndAggregate)
            {
                throw new AggregateException("One or more async handlers threw exceptions", exceptions);
            }

            return tasks.Count > 0;
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
                    HandleError(ex, message, messageType, exceptions);

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

        private void HandleError(Exception ex, object message, Type messageType, List<Exception> exceptions)
        {
            // Unwrap TargetInvocationException from reflection-based invocation (weak references)
            // so users get the original exception, not the reflection wrapper
            var actualException = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                ? tie.InnerException
                : ex;

            var errorArgs = new HandlerErrorEventArgs(actualException, message, messageType);

            var handler = HandlerError;
            handler?.Invoke(this, errorArgs);

            if (_errorPolicy == ErrorPolicy.ContinueAndAggregate)
            {
                exceptions.Add(actualException);
            }
            else if (_errorPolicy == ErrorPolicy.StopOnFirstError)
            {
                throw actualException;
            }
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
            _stringHandlersLock.Dispose();
        }

        #endregion
    }
}