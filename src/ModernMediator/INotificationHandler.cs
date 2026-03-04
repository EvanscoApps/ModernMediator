using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Defines a handler for a notification.
    /// Unlike request handlers, multiple notification handlers can exist for the same notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being handled.</typeparam>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the notification.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Handle(TNotification notification, CancellationToken cancellationToken = default);
    }
}
