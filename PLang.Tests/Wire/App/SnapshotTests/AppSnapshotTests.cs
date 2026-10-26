namespace PLang.Tests.App.SnapshotTests;

public class AppSnapshotTests
{
    [Test]
    public async Task App_Snapshot_StoresEachPart_AtItsMemberPath()
    {
        // The app is the root part (its property; its mode is how it was started, not stored); each part under it is at
        // its member path from the app.
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        await app.actor.list.User.Context.Variable.Set("x", 1);
        await app.actor.list.System.Context.Variable.Set("y", 2);
        var snap = app.Snapshot(app.actor.list.User.Context).Saved();

        await Assert.That(snap.Entries.Get("mode", snap.Context)).IsNull();
        await Assert.That(snap.Holds("property")).IsFalse();                  // nothing added: an empty node is left out
        await Assert.That(snap.Holds("Code")).IsTrue();
        await Assert.That(snap.Holds(SnapshotPathExtensions.UserVariable)).IsTrue();
        await Assert.That(snap.Holds("actor.list.System.Context.Variable")).IsTrue();   // both actors' contexts are saved
        await Assert.That(snap.Holds(SnapshotPathExtensions.UserCall)).IsFalse();       // nothing running: no call to save
    }

    [Test]
    public async Task App_Snapshot_InsideATask_IsRefused()
    {
        // a task's context is no member of anything the walk reaches: its variables and frames would be missing
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        using var task = user.Context.Child(user, CancellationToken.None);

        var saved = app.Snapshot(task);

        await Assert.That(saved.Success).IsFalse();
        await Assert.That(saved.Error!.Key).IsEqualTo("SnapshotInTask");
    }

    [Test]
    public async Task App_Restore_DispatchesEachSubtree_ToMatchingThisRestore()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        await src.actor.list.User.Context.Variable.Set("x", 1);
        src.Build = new global::app.module.build.@this(src.actor.list.System.Context);   // building — Mode is Build

        var snap = src.Snapshot(src.actor.list.User.Context).Saved();

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();   // testing — and it stays so
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That((await (await dst.actor.list.User.Context.Variable.Get("x")).Value())?.ToString()).IsEqualTo("1");
        await Assert.That(dst.Build == null).IsTrue();
        await Assert.That(dst.test.list.Session != null).IsTrue();
    }

    [Test]
    public async Task App_Snapshot_OmitsReconstructOnBuildSubsystems()
    {
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var snap = app.Snapshot(app.actor.list.User.Context).Saved();

        // a member that is no part, or a concept whose list is no part (goals, tests, errors, …), leaves no node
        foreach (var name in new[] { "goal", "module", "type", "test", "error", "variable", "shortcut", "store", "filesystem", "Cache", "parent", "Build" })
            await Assert.That(snap.Holds(name)).IsFalse();
    }

    [Test]
    public async Task App_Cache_NotInSnapshot_FreshAppHasEmptyCache()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var snap = src.Snapshot(src.actor.list.User.Context).Saved();

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That(dst.Cache).IsTypeOf<global::app.module.cache.Memory>();
        await Assert.That(snap.Holds("Cache")).IsFalse();
    }
}
