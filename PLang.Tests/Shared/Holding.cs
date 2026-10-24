namespace PLang.Tests;

/// <summary>Where a test binds while it runs, as a program's on.event does: on the hold of its app's user, never on the
/// item — a type entry, a goal type or a module is its parent's, shared by every test app made from it.</summary>
public static class Holding
{
    /// <summary>The events <paramref name="item"/> has held on <paramref name="app"/>'s user's context — what a
    /// binding made in that app's run is bound on.</summary>
    public static global::app.@event.on.own HeldIn(this global::app.type.item.@this item, global::app.@this app)
        => app.actor.list.User.Context.on[item];
}
