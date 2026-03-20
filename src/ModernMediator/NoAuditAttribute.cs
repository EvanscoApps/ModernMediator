namespace ModernMediator;

/// <summary>
/// Suppresses audit recording for the decorated request. Apply to query requests
/// or non-sensitive commands that do not require an audit trail.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NoAuditAttribute : Attribute { }
