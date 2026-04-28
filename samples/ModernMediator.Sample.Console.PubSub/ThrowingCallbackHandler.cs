using System;

namespace ModernMediator.Sample.Console.PubSub;

/// <summary>
/// A notification handler subscribed via mediator.Subscribe&lt;T&gt;(handler.Handle) that
/// deliberately throws. Demonstrates the callback path of the v2.2 unified error-handling
/// surface: when this handler throws, the mediator's HandlerError event fires with
/// HandlerType pointing to this class and HandlerInstance pointing to the subscribed
/// instance, even though the handler is wired via Subscribe rather than DI.
/// </summary>
/// <remarks>
/// The Handle method shape (void, no CancellationToken) matches the other callback-path
/// handlers in this sample (DogResponseHandler, CatResponseHandler, etc.) so the
/// Subscribe&lt;T&gt;(Action&lt;T&gt;) overload picks it up. The class deliberately does NOT
/// implement INotificationHandler&lt;T&gt; so it cannot be confused with a DI-resolved
/// handler.
/// </remarks>
public sealed class ThrowingCallbackHandler
{
    public void Handle(ProblematicSightingNotification notification)
    {
        throw new InvalidOperationException(
            "ThrowingCallbackHandler also refuses to handle '" + notification.Description + "'.");
    }
}
