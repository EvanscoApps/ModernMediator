namespace ModernMediator
{
    /// <summary>
    /// Represents failure information for a <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="Code">A machine-readable error code.</param>
    /// <param name="Message">A human-readable error message.</param>
    public readonly record struct ResultError(string Code, string Message)
    {
        /// <summary>
        /// Represents the absence of an error. Used as the <see cref="Result{T}.Error"/>
        /// value on successful results.
        /// </summary>
        public static readonly ResultError None = new(string.Empty, string.Empty);
    }
}
