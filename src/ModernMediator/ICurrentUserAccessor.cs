namespace ModernMediator;

/// <summary>
/// Provides the identity of the user associated with the current execution context.
/// Used by <c>AuditBehavior</c> to capture who dispatched a request.
/// </summary>
/// <remarks>
/// The default implementation resolves identity from <c>IHttpContextAccessor</c>
/// and is registered automatically when <c>UseHttpContextIdentity()</c> is called
/// during <c>AddAudit()</c> registration. If no implementation is registered,
/// <see cref="UserId"/> and <see cref="UserName"/> will both be <c>null</c>.
/// </remarks>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// Gets the unique identifier of the current user, typically the value of
    /// the <c>NameIdentifier</c> claim. Returns <c>null</c> if no identity
    /// is available.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the display name of the current user, typically the value of
    /// <c>IIdentity.Name</c>. Returns <c>null</c> if no identity is available.
    /// </summary>
    string? UserName { get; }
}
