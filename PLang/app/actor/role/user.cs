using Verb = global::app.type.item.permission.Verb;

namespace app.actor.role;

/// <summary>A user of the app: it reads and runs what is in the runtime's shared os folder (the builder's goals,
/// /system/error/show); writing there is the system's alone.</summary>
internal sealed class user : @this
{
    internal override string Name => "User";

    internal override global::app.actor.@this In(global::app.actor.list.@this list) => list.User;

    internal override void Stand(global::app.actor.permission.@this view, global::app.@this app)
        => view.Stand(app.filesystem.os, Verb.read, Verb.execute);
}
