using System;

namespace ModernMediator
{
    /// <summary>
    /// Specifies a maximum execution duration for a request handler.
    /// When <see cref="TimeoutBehavior{TRequest, TResponse}"/> is registered in the pipeline,
    /// requests decorated with this attribute will be cancelled if the handler does not
    /// complete within the specified duration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TimeoutAttribute : Attribute
    {
        /// <summary>
        /// Gets the maximum allowed execution time in milliseconds.
        /// </summary>
        public int Milliseconds { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutAttribute"/> class.
        /// </summary>
        /// <param name="milliseconds">
        /// The maximum allowed execution time in milliseconds. Must be greater than zero.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="milliseconds"/> is zero or negative.
        /// </exception>
        public TimeoutAttribute(int milliseconds)
        {
            if (milliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "Timeout must be greater than zero.");

            Milliseconds = milliseconds;
        }
    }
}
