namespace PLang.Tests.App.SnapshotTests;

public class AppSnapshotTests
{
    [Test]
    public async Task App_Snapshot_WalksISnapshottedProperties_AndAggregatesIntoTree()
    {
        var app = new global::app.@this("/test");
        var snap = app.Snapshot();

        await Assert.That(snap.HasSection("Variables")).IsTrue();
        await Assert.That(snap.HasSection("Errors")).IsTrue();
        await Assert.That(snap.HasSection("Providers")).IsTrue();
        await Assert.That(snap.HasSection("Statics")).IsTrue();
        await Assert.That(snap.HasSection("Build")).IsTrue();
        await Assert.That(snap.HasSection("Testing")).IsTrue();
        await Assert.That(snap.HasSection("CallStack")).IsTrue();
    }

    [Test]
    public async Task App_Restore_DispatchesEachSubtree_ToMatchingThisRestore()
    {
        var src = new global::app.@this("/src");
        src.User.Context.Variable.Set("x", 1);
        src.Builder.IsEnabled = true;
        src.Tester.IsEnabled = true;

        var snap = src.Snapshot();

        var dst = new global::app.@this("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That((await dst.User.Context.Variable.Get("x").Value())).IsEqualTo(1);
        await Assert.That(dst.Builder.IsEnabled).IsTrue();
        await Assert.That(dst.Tester.IsEnabled).IsTrue();
    }

    [Test]
    public async Task App_Snapshot_OmitsReconstructOnBuildSubsystems()
    {
        var app = new global::app.@this("/test");
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
        var src = new global::app.@this("/src");
        var snap = src.Snapshot();

        var dst = new global::app.@this("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Cache).IsTypeOf<global::app.module.cache.Memory>();
        await Assert.That(snap.HasSection("Cache")).IsFalse();
    }
}
