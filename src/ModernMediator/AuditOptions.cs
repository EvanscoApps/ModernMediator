using System;

namespace ModernMediator;

/// <summary>
/// Configuration options for <see cref="AuditBehavior{TRequest,TResponse}"/>.
/// Configure via <c>AddAudit()</c> at registration time.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>
    /// Gets or sets the dispatch mode for audit writes.
    /// Defaults to <see cref="AuditDispatchMode.Channel"/> (background channel drainer).
    /// Set to <see cref="AuditDispatchMode.FireAndForget"/> only when the audit writer
    /// has no persistence concern (e.g. a structured logger). See ADR-001.
    /// </summary>
    public AuditDispatchMode DispatchMode { get; set; } = AuditDispatchMode.Channel;

    /// <summary>
    /// Gets or sets the maximum number of audit records the channel can buffer
    /// before backpressure is applied. Defaults to 1024.
    /// Only applies when <see cref="DispatchMode"/> is
    /// <see cref="AuditDispatchMode.Channel"/>.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1024;
}
