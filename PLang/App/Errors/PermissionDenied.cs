using PermissionRecord = global::App.FileSystem.Permission.@this;

namespace App.Errors;

/// <summary>
/// Raised by <c>Path.Authorize</c> when the actor refuses a permission ("n"
/// answer). Carries the constructed Permission the request would have needed —
/// callers can render the missing grant, surface it to the actor, or audit.
/// </summary>
public sealed class PermissionDenied : Error
{
    public override ErrorCategory Category => ErrorCategory.Application;

    /// <summary>The Permission record that was denied.</summary>
    public PermissionRecord Permission { get; }

    public PermissionDenied(PermissionRecord permission)
        : base($"Permission denied: {permission.Actor} on {permission.Path}", "PermissionDenied", 403)
    {
        Permission = permission;
    }
}
