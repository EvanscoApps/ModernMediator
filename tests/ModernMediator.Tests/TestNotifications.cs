using ModernMediator;

namespace ModernMediator.Tests
{
    public sealed record TestNotification(string Payload) : INotification;
    public sealed record OtherTestNotification(int Value) : INotification;
}
