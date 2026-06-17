using permission = global::app.type.permission.@this;

namespace app.error;

/// <summary>
/// Raised by <c>path.Authorize</c> when the actor refuses a permission ("n"
/// answer). Carries the constructed Permission the request would have needed —
/// callers can render the missing grant, surface it to the actor, or audit.
/// </summary>
public sealed class PermissionDenied : Error
{
    public override ErrorCategory Category => ErrorCategory.Application;

    /// <summary>The permission grant that was denied.</summary>
    public permission Permission { get; }

    public PermissionDenied(permission grant)
        : base($"Permission denied: {grant.Actor} on {grant.Path}", "PermissionDenied", 403)
    {
        Permission = grant;
    }
}
