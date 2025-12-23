namespace ModernMediator
{
    /// <summary>
    /// Result of an exception handler indicating whether the exception was handled.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public readonly struct ExceptionHandlingResult<TResponse>
    {
        /// <summary>
        /// Gets whether the exception was handled.
        /// </summary>
        public bool Handled { get; }

        /// <summary>
        /// Gets the response to return if the exception was handled.
        /// </summary>
        public TResponse? Response { get; }

        private ExceptionHandlingResult(bool handled, TResponse? response)
        {
            Handled = handled;
            Response = response;
        }

        /// <summary>
        /// Creates a result indicating the exception was handled with the given response.
        /// </summary>
        public static ExceptionHandlingResult<TResponse> Handle(TResponse response) =>
            new(true, response);

        /// <summary>
        /// Creates a result indicating the exception was not handled and should bubble up.
        /// </summary>
        public static ExceptionHandlingResult<TResponse> NotHandled() =>
            new(false, default);
    }
}