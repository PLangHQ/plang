namespace app.actor.role;

/// <summary>
/// What an actor is in its app — the app's system, or a user: the name the actor goes by, and the grants it stands on
/// its app's folders, which its permission view holds from the first time it is asked for. Each kind answers for itself.
/// </summary>
internal abstract class @this
{
    /// <summary>The name an actor of this role goes by (<c>System</c>, <c>User</c>): the word written into the grants it
    /// signs and stores, and shown in its prompts. Not the choice a step names an actor by (<see cref="actor.Name"/>,
    /// <c>actor=system</c>): that is what a program writes; this is what a stored grant holds, which changing would
    /// change.</summary>
    internal abstract string Name { get; }

    /// <summary>The actor of this role in <paramref name="list"/>: another app's actor that stands where this one does.</summary>
    internal abstract global::app.actor.@this In(global::app.actor.list.@this list);

    /// <summary>Stands this role's grants on <paramref name="app"/>'s folders in <paramref name="view"/>, as it is made.</summary>
    internal abstract void Stand(global::app.actor.permission.@this view, global::app.@this app);
}
