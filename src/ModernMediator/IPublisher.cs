using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    /// <summary>
    /// Publishes a notification to DI-resolved <see cref="INotificationHandler{TNotification}"/> instances.
    /// This is one of the two notification dispatch paths in ModernMediator; the other path, runtime callback
    /// dispatch, is reached via the <c>Publish</c>/<c>PublishAsync</c> overloads declared on <see cref="IMediator"/>.
    /// </summary>
    public interface IPublisher
    {
        /// <summary>
        /// Publishes a notification to DI-resolved <see cref="INotificationHandler{TNotification}"/> instances.
        /// Does not invoke runtime <c>Subscribe</c>/<c>SubscribeAsync</c> callbacks; those are reached via
        /// the <c>Publish</c>/<c>PublishAsync</c> overloads on <see cref="IMediator"/>.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification.</typeparam>
        /// <param name="notification">The notification to publish.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification;
    }
}
