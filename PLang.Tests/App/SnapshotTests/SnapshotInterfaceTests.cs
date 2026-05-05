namespace PLang.Tests.App.SnapshotTests;

public class SnapshotInterfaceTests
{
    [Test]
    public async Task ISnapshotted_Capture_AppendsTypedEntries_ToSnapshot()
    {
        // Capture writes typed entries; Restore reads them back in order.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ISnapshotted_RestoreIsStaticFactory_NotInstanceMethod()
    {
        // Pins the `static abstract Restore(Snapshot.@this s, Context.@this ctx)` shape.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
