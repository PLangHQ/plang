using Verb = global::app.type.item.permission.Verb;

namespace app.actor.role;

/// <summary>The app's system: it reads, runs, writes and deletes in the runtime's shared os folder, and the stores' folder
/// is its alone (it opens the stores there; no other actor reaches it).</summary>
internal sealed class system : @this
{
    internal override string Name => "System";

    internal override global::app.actor.@this In(global::app.actor.list.@this list) => list.System;

    internal override void Stand(global::app.actor.permission.@this view, global::app.@this app)
    {
        view.Stand(app.filesystem.os, Verb.read, Verb.execute, Verb.write, Verb.delete);
        view.Stand(app.filesystem.store, Verb.read, Verb.write, Verb.delete);
    }
}
