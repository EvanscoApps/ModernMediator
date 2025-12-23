namespace ModernMediator.Dispatchers
{
    /// <summary>
    /// Interface for UI thread dispatchers.
    /// Implement this for platform-specific UI thread marshalling.
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Returns true if the current thread is the UI thread.
        /// </summary>
        bool CheckAccess();

        /// <summary>
        /// Invokes an action synchronously on the UI thread.
        /// </summary>
        void Invoke(Action action);

        /// <summary>
        /// Invokes a function asynchronously on the UI thread.
        /// </summary>
        Task InvokeAsync(Func<Task> func);
    }
}
