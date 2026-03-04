namespace ModernMediator
{
    /// <summary>
    /// Extension methods for creating <see cref="Result{T}"/> instances.
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>
        /// Wraps the value in a successful <see cref="Result{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="value">The value to wrap.</param>
        /// <returns>A success result containing the value.</returns>
        public static Result<T> ToResult<T>(this T value)
        {
            return Result<T>.Success(value);
        }

        /// <summary>
        /// Wraps the error in a failed <see cref="Result{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="error">The error to wrap.</param>
        /// <returns>A failure result containing the error.</returns>
        public static Result<T> ToFailedResult<T>(this ResultError error)
        {
            return Result<T>.Failure(error);
        }
    }
}
