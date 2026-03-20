using System;
using System.Collections.Generic;
using System.IO;

namespace ModernMediator;

/// <summary>
/// Configuration options for <see cref="RetryBehavior{TRequest,TResponse}"/>.
/// Specifies which exception types are eligible for retry. Configure via
/// <c>AddRetry()</c> at registration time.
/// </summary>
public sealed class RetryOptions
{
    private readonly List<Type> _retryableExceptionTypes = new()
    {
        typeof(TimeoutException),
        typeof(IOException)
    };

    /// <summary>
    /// Gets the list of exception types that will trigger a retry attempt.
    /// Defaults to <see cref="TimeoutException"/> and <see cref="IOException"/>.
    /// <see cref="OperationCanceledException"/> is never retried regardless of
    /// this list.
    /// </summary>
    public IReadOnlyList<Type> RetryableExceptionTypes => _retryableExceptionTypes;

    /// <summary>
    /// Adds an exception type to the list of retryable exceptions.
    /// </summary>
    public RetryOptions RetryOn<TException>() where TException : Exception
    {
        _retryableExceptionTypes.Add(typeof(TException));
        return this;
    }
}
