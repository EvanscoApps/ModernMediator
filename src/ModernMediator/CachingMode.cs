namespace ModernMediator
{
    /// <summary>
    /// Controls when handler wrappers and lookups are initialized.
    /// </summary>
    public enum CachingMode
    {
        /// <summary>
        /// All handler wrappers and lookups are initialized on first Mediator access.
        /// Best for long-running applications where startup cost is amortized.
        /// This is the default.
        /// </summary>
        Eager,

        /// <summary>
        /// Handler wrappers are initialized on-demand as messages are processed.
        /// Best for cold start scenarios (serverless, Native AOT) where minimal initialization is preferred.
        /// </summary>
        Lazy
    }
}