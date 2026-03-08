using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// A pipeline behavior that enforces per-request timeout limits declared via
    /// <see cref="TimeoutAttribute"/>. When <typeparamref name="TRequest"/> is decorated
    /// with <c>[Timeout(ms)]</c>, the behavior creates a linked
    /// <see cref="CancellationTokenSource"/> that cancels after the specified duration.
    /// If no <see cref="TimeoutAttribute"/> is present, the behavior delegates to <c>next(request, ct)</c>
    /// with zero overhead.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <remarks>
    /// This behavior uses <see cref="MemberInfo.GetCustomAttribute{T}()"/> on
    /// <c>typeof(TRequest)</c> to read the <see cref="TimeoutAttribute"/>. The result is
    /// cached per request type in a static <see cref="ConcurrentDictionary{TKey, TValue}"/>,
    /// so the reflection cost is incurred only once per request type for the lifetime of the
    /// application. This is an accepted tradeoff for attribute-driven timeout configuration.
    /// </remarks>
    public class TimeoutBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static readonly ConcurrentDictionary<Type, int?> TimeoutCache = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutBehavior{TRequest, TResponse}"/> class.
        /// </summary>
        public TimeoutBehavior()
        {
        }

        /// <inheritdoc />
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var timeoutMs = TimeoutCache.GetOrAdd(typeof(TRequest), static type =>
            {
                var attr = type.GetCustomAttribute<TimeoutAttribute>();
                return attr?.Milliseconds;
            });

            if (timeoutMs == null)
                return await next(request, cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs.Value);
            return await next(request, cts.Token).WaitAsync(cts.Token);
        }
    }
}
