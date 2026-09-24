using permission = global::app.type.item.permission.@this;

namespace app.error;

/// <summary>
/// Raised by <c>path.Authorize</c> when no consent is had — the actor refuses ("n"), or nobody can
/// answer (a closed input; that channel failure rides as the cause). Carries the constructed
/// Permission the request would have needed —
/// callers can render the missing grant, surface it to the actor, or audit.
/// </summary>
public sealed class PermissionDenied : Error
{

    /// <summary>The permission grant that was denied.</summary>
    public permission Permission { get; }

    public PermissionDenied(permission grant)
        : base($"Permission denied: {grant.Actor} on {grant.Path}", "PermissionDenied", 403)
    {
        Permission = grant;
    }
}
