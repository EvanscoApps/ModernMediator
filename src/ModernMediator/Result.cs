using System;

namespace ModernMediator
{
    /// <summary>
    /// A value type that represents either a successful outcome carrying a value of type
    /// <typeparamref name="T"/>, or a failed outcome carrying a <see cref="ResultError"/>.
    /// Use as the <c>TResponse</c> in <c>IRequest&lt;Result&lt;T&gt;&gt;</c> to avoid throwing
    /// exceptions for expected failures.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    public readonly struct Result<T> : IResult
    {
        private readonly T _value;
        private readonly ResultError _error;
        private readonly bool _isSuccess;

        private Result(T value)
        {
            _value = value;
            _error = ResultError.None;
            _isSuccess = true;
        }

        private Result(ResultError error)
        {
            _value = default!;
            _error = error;
            _isSuccess = false;
        }

        /// <summary>
        /// Gets a value indicating whether the result represents a successful outcome.
        /// </summary>
        public bool IsSuccess => _isSuccess;

        /// <summary>
        /// Gets a value indicating whether the result represents a failed outcome.
        /// </summary>
        public bool IsFailure => !_isSuccess;

        /// <summary>
        /// Gets the success value.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when accessed on a failed result.
        /// </exception>
        public T Value => _isSuccess
            ? _value
            : throw new InvalidOperationException("Cannot access Value of a failed Result.");

        /// <summary>
        /// Gets the error information. Returns <see cref="ResultError.None"/> on a success result.
        /// </summary>
        public ResultError Error => _error;

        /// <summary>
        /// Creates a successful result carrying the specified value.
        /// </summary>
        /// <param name="value">The success value.</param>
        /// <returns>A success result.</returns>
        public static Result<T> Success(T value) => new(value);

        /// <summary>
        /// Creates a failed result carrying the specified error.
        /// </summary>
        /// <param name="error">The error information.</param>
        /// <returns>A failure result.</returns>
        public static Result<T> Failure(ResultError error) => new(error);

        /// <summary>
        /// Creates a failed result with the specified error code and message.
        /// </summary>
        /// <param name="code">A machine-readable error code.</param>
        /// <param name="message">A human-readable error message.</param>
        /// <returns>A failure result.</returns>
        public static Result<T> Failure(string code, string message) => new(new ResultError(code, message));

        /// <summary>
        /// Implicitly converts a value of type <typeparamref name="T"/> to a success result.
        /// </summary>
        /// <param name="value">The success value.</param>
        public static implicit operator Result<T>(T value) => Success(value);

        /// <summary>
        /// Implicitly converts a <see cref="ResultError"/> to a failure result.
        /// </summary>
        /// <param name="error">The error information.</param>
        public static implicit operator Result<T>(ResultError error) => Failure(error);

        /// <summary>
        /// Transforms the success value using the specified mapper function.
        /// If this result is a failure, the error is propagated without calling <paramref name="mapper"/>.
        /// </summary>
        /// <typeparam name="TOut">The type of the mapped value.</typeparam>
        /// <param name="mapper">A function to transform the success value.</param>
        /// <returns>A new result containing the mapped value, or the original error.</returns>
        public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
        {
            return _isSuccess
                ? Result<TOut>.Success(mapper(_value))
                : Result<TOut>.Failure(_error);
        }

        /// <summary>
        /// Returns the success value, or <paramref name="defaultValue"/> if this result is a failure.
        /// </summary>
        /// <param name="defaultValue">The value to return when the result is a failure.</param>
        /// <returns>The success value or the default.</returns>
        public T GetValueOrDefault(T defaultValue = default!)
        {
            return _isSuccess ? _value : defaultValue;
        }

        /// <summary>
        /// Returns a string representation of this result.
        /// </summary>
        /// <returns><c>"Success(&lt;value&gt;)"</c> or <c>"Failure(&lt;Code&gt;: &lt;Message&gt;)"</c>.</returns>
        public override string ToString()
        {
            return _isSuccess
                ? $"Success({_value})"
                : $"Failure({_error.Code}: {_error.Message})";
        }
    }
}
