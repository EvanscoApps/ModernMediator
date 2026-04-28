using System;
using ModernMediator;

namespace ModernMediator.Sample.Console.PubSub;

/// <summary>
/// A custom ISubscriberExceptionSink that prints any contained subscriber exception to
/// the console. Demonstrates ADR-005's containment guarantee: when a HandlerError event
/// subscriber itself throws, the mediator does not crash; the exception is routed to the
/// configured ISubscriberExceptionSink instead.
/// </summary>
public sealed class ConsoleSubscriberExceptionSink : ISubscriberExceptionSink
{
    public void OnSubscriberException(Exception exception)
    {
        // Sink implementations must not throw; per ADR-005 the mediator's containment
        // contract relies on this. A plain Console.WriteLine inside a try/catch is safe.
        try
        {
            System.Console.WriteLine(
                "  [ConsoleSubscriberExceptionSink] A HandlerError subscriber threw: " +
                exception.GetType().Name + ": " + exception.Message);
        }
        catch
        {
            // Intentionally swallow. A sink that itself throws breaks the containment
            // guarantee for downstream consumers.
        }
    }
}
