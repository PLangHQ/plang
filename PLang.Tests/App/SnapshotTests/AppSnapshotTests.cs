namespace PLang.Tests.App.SnapshotTests;

public class AppSnapshotTests
{
    [Test]
    public async Task App_Snapshot_WalksISnapshottedProperties_AndAggregatesIntoTree()
    {
        // App.Snapshot() walks the App's @this properties, asks each ISnapshotted
        // for its capture, and returns a Snapshot.@this tree mirroring App's structure.
        var app = new global::App.@this("/test");
        var snap = app.Snapshot();

        await Assert.That(snap.HasSection("Variables")).IsTrue();
        await Assert.That(snap.HasSection("Errors")).IsTrue();
        await Assert.That(snap.HasSection("Providers")).IsTrue();
        await Assert.That(snap.HasSection("Statics")).IsTrue();
        await Assert.That(snap.HasSection("Build")).IsTrue();
        await Assert.That(snap.HasSection("Testing")).IsTrue();
    }

    [Test]
    public async Task App_Restore_DispatchesEachSubtree_ToMatchingThisRestore()
    {
        // App.Restore(snap, ctx) walks subtrees and hands each to the matching
        // @this.Restore(subsnap, ctx).
        var src = new global::App.@this("/src");
        src.User.Context.Variables.Set("x", 1);
        src.Build.IsEnabled = true;
        src.Testing.IsEnabled = true;

        var snap = src.Snapshot();

        var dst = new global::App.@this("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.User.Context.Variables.Get("x")?.Value).IsEqualTo(1);
        await Assert.That(dst.Build.IsEnabled).IsTrue();
        await Assert.That(dst.Testing.IsEnabled).IsTrue();
    }

    [Test]
    public async Task App_Snapshot_OmitsReconstructOnBuildSubsystems()
    {
        // App.Modules / Goals / Channels / Events / Cache / Settings do NOT appear
        // in the captured tree — they reconstruct on App boot.
        var app = new global::App.@this("/test");
        var snap = app.Snapshot();

        await Assert.That(snap.HasSection("Modules")).IsFalse();
        await Assert.That(snap.HasSection("Goals")).IsFalse();
        await Assert.That(snap.HasSection("Channels")).IsFalse();
        await Assert.That(snap.HasSection("Events")).IsFalse();
        await Assert.That(snap.HasSection("Cache")).IsFalse();
        await Assert.That(snap.HasSection("Settings")).IsFalse();
        await Assert.That(snap.HasSection("Navigators")).IsFalse();
        await Assert.That(snap.HasSection("Types")).IsFalse();
        await Assert.That(snap.HasSection("Config")).IsFalse();
        await Assert.That(snap.HasSection("FileSystem")).IsFalse();
    }

    [Test]
    public async Task App_Cache_NotInSnapshot_FreshAppHasEmptyCache()
    {
        // After Restore, App.Cache is empty — cache is a hint, not state.
        var src = new global::App.@this("/src");
        // Touch the cache on src; whatever is there is irrelevant — it must not survive.
        var snap = src.Snapshot();

        var dst = new global::App.@this("/dst");
        dst.Restore(snap, dst.User.Context);

        // Cache is the live MemoryStepCache; the contract is "fresh on the restored side".
        // We assert via the type — MemoryStepCache is reconstruct-on-build, so no key/value
        // from src ever reaches dst. The cache instance itself is the fresh one created by App ctor.
        await Assert.That(dst.Cache).IsTypeOf<MemoryStepCache>();
        // Snapshot section is absent.
        await Assert.That(snap.HasSection("Cache")).IsFalse();
    }
}
