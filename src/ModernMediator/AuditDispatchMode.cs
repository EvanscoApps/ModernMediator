namespace ModernMediator;

/// <summary>
/// Controls how <see cref="AuditBehavior{TRequest,TResponse}"/> dispatches
/// audit record writes to the configured <see cref="IAuditWriter"/>.
/// </summary>
public enum AuditDispatchMode
{
    /// <summary>
    /// Audit records are enqueued into a <see cref="System.Threading.Channels.Channel{T}"/>
    /// and written by a hosted background service. Provides better durability on
    /// graceful shutdown. This is the default. See ADR-001.
    /// </summary>
    Channel,

    /// <summary>
    /// Audit records are written fire-and-forget without awaiting completion.
    /// Failures are logged but do not propagate. Use only when the audit writer
    /// has no persistence concern.
    /// </summary>
    FireAndForget
}
