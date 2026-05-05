namespace PLang.Tests.App.SnapshotTests;

public class AppSnapshotTests
{
    [Test]
    public async Task App_Snapshot_WalksISnapshottedProperties_AndAggregatesIntoTree()
    {
        // App.Snapshot() walks the App's @this properties, asks each ISnapshotted
        // for its capture, and returns a Snapshot.@this tree mirroring App's structure.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task App_Restore_DispatchesEachSubtree_ToMatchingThisRestore()
    {
        // App.Restore(snap, ctx) walks subtrees and hands each to the matching
        // @this.Restore(subsnap, ctx).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task App_Snapshot_OmitsReconstructOnBuildSubsystems()
    {
        // App.Modules / Goals / Channels / Events / Cache / Settings do NOT appear
        // in the captured tree — they reconstruct on App boot.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task App_Cache_NotInSnapshot_FreshAppHasEmptyCache()
    {
        // After Restore, App.Cache is empty — cache is a hint, not state.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
