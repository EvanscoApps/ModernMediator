using System;

namespace ModernMediator.Idempotency.EntityFramework;

/// <summary>
/// Represents a persisted idempotency entry in the durable store.
/// The <see cref="Fingerprint"/> column carries a unique constraint enforced
/// at the database level, guaranteeing exactly-once execution regardless of
/// application-layer race conditions. See ADR-004.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>
    /// Gets or sets the SHA-256 fingerprint of the request.
    /// This column has a unique constraint, so duplicate inserts are rejected
    /// by the database.
    /// </summary>
    public required string Fingerprint { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized cached response payload.
    /// </summary>
    public required string SerializedResponse { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified response type name, used for
    /// deserialization on cache retrieval.
    /// </summary>
    public required string ResponseTypeName { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp at which this entry was written.
    /// </summary>
    public required DateTimeOffset CachedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp at which this entry expires and
    /// is eligible for cleanup.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; set; }
}
