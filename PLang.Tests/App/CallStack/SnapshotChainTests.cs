namespace PLang.Tests.App.CallStack;

// Call.SnapshotChain returns [this, Caller, Caller.Caller, ..., Root].
// Used by App.Run to attach a chain to ServiceError on exception.
// Stable refs only — no copy.
public class SnapshotChainTests
{
    [Test]
    public async Task SnapshotChain_FirstElementIsSelf()
    {
        // chain[0] == this — the failing call IS in the chain (behavior tweak vs old shape).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotChain_OrderIsLeafToRoot()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotChain_SingleFrame_ReturnsLengthOne()
    {
        // Root call with no Caller: chain has exactly one element.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotChain_ReturnsStableRefs_NoCopy()
    {
        // ReferenceEquals(chain[0], theCall) — no defensive copying.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotChain_DoesNotWalkCause()
    {
        // Sibling Calls linked via Cause are not in the chain — only Caller chain is.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
