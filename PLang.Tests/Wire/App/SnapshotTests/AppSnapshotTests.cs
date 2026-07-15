namespace PLang.Tests.App.SnapshotTests;

public class AppSnapshotTests
{
    [Test]
    public async Task App_Snapshot_WalksISnapshottedProperties_AndAggregatesIntoTree()
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        // Build/Test are presence-based — a section appears only when the mode is on.
        // TestApp enables Test; turn Build on too so both sections show up.
        app.Build = new global::app.module.action.build.@this(app.System.Context);
        var snap = app.Snapshot(app.User.Context);

        await Assert.That(snap.HasSection("Variables")).IsTrue();
        await Assert.That(snap.HasSection("Errors")).IsTrue();
        await Assert.That(snap.HasSection("Providers")).IsTrue();
        await Assert.That(snap.HasSection("Statics")).IsTrue();
        await Assert.That(snap.HasSection("Build")).IsTrue();
        await Assert.That(snap.HasSection("Test")).IsTrue();
        await Assert.That(snap.HasSection("CallStack")).IsTrue();
    }

    [Test]
    public async Task App_Restore_DispatchesEachSubtree_ToMatchingThisRestore()
    {
        var src = global::PLang.Tests.TestApp.Create("/src");
        src.User.Context.Variable.Set("x", 1);
        src.Build = new global::app.module.action.build.@this(src.System.Context);
        src.Test = new global::app.test.list.@this(src.System.Context);

        var snap = src.Snapshot(src.User.Context);

        var dst = global::PLang.Tests.TestApp.Create("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That((await (await dst.User.Context.Variable.Get("x")).Value())?.ToString()).IsEqualTo("1");
        await Assert.That(dst.Build != null).IsTrue();
        await Assert.That(dst.Test != null).IsTrue();
    }

    [Test]
    public async Task App_Snapshot_OmitsReconstructOnBuildSubsystems()
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        var snap = app.Snapshot(app.User.Context);

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
        var src = global::PLang.Tests.TestApp.Create("/src");
        var snap = src.Snapshot(src.User.Context);

        var dst = global::PLang.Tests.TestApp.Create("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Cache).IsTypeOf<global::app.module.action.cache.Memory>();
        await Assert.That(snap.HasSection("Cache")).IsFalse();
    }
}
