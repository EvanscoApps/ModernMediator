using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// </summary>
    public interface IPublisher
    {
        /// <summary>
        /// Publishes a notification to all registered <see cref="INotificationHandler{TNotification}"/> instances.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification.</typeparam>
        /// <param name="notification">The notification to publish.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification;
    }
}
