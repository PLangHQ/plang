namespace app.actor.role;

/// <summary>
/// What an actor is in its app — the app's system, or a user: the name the actor goes by, and the grants it stands on
/// its app's folders, which its permission view holds from the first time it is asked for. Each kind answers for itself.
/// </summary>
internal abstract class @this
{
    /// <summary>The name an actor of this role goes by (<c>System</c>, <c>User</c>).</summary>
    internal abstract string Name { get; }

    /// <summary>Stands this role's grants on <paramref name="app"/>'s folders in <paramref name="view"/>, as it is made.</summary>
    internal abstract void Stand(global::app.actor.permission.@this view, global::app.@this app);
}
