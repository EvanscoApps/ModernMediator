using System.Threading.Tasks;

namespace ModernMediator.Internal
{
    internal readonly record struct TrackedHandlerTask<TEntry>(Task Task, TEntry Entry);
}
