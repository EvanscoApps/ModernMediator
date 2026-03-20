namespace ModernMediator;

/// <summary>
/// Represents a single audit entry recorded by <c>AuditBehavior</c> for a
/// dispatched request.
/// </summary>
public sealed class AuditRecord
{
    /// <summary>Gets the fully qualified type name of the dispatched request.</summary>
    public required string RequestTypeName { get; init; }

    /// <summary>Gets the JSON-serialized payload of the dispatched request.</summary>
    public required string SerializedPayload { get; init; }

    /// <summary>
    /// Gets the unique identifier of the user who dispatched the request,
    /// or <c>null</c> if identity was not available.
    /// </summary>
    public required string? UserId { get; init; }

    /// <summary>
    /// Gets the display name of the user who dispatched the request,
    /// or <c>null</c> if identity was not available.
    /// </summary>
    public required string? UserName { get; init; }

    /// <summary>Gets the UTC timestamp at which the request was dispatched.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether the handler completed without throwing.
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the exception message if the handler threw, or <c>null</c> if the
    /// handler succeeded.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>Gets the total time elapsed during handler execution.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets an optional correlation identifier propagated from the request context,
    /// or <c>null</c> if no correlation id was available.
    /// </summary>
    public string? CorrelationId { get; init; }
}
