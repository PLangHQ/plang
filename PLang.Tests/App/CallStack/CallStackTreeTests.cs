namespace PLang.Tests.App.CallStack;

// Push/Pop tree mechanics on the new App.CallStack.@this (AsyncLocal-rooted, no ConcurrentStack).
public class CallStackTreeTests
{
    [Test]
    public async Task Push_SetsCurrentToNewCall()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_NestedPush_SetsCallerToOuter()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_AppendsCallToCallerChildren()
    {
        // outer.Children contains the inner Call after a nested Push.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Pop_RestoresCurrentToCaller()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Pop_RemovesFromCallerChildren_WhenHistoryFalse()
    {
        // Default flags (history:false): Children is empty after the child disposes.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Pop_RetainsInCallerChildren_WhenHistoryTrue()
    {
        // Flags{History=true}: popped Call stays in Caller.Children.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Root_IsFirstPushedCall()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Root_NullBeforeAnyPush()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task MaxFrames_FifoEvictsOldestSibling_WhenHistoryTrue()
    {
        // history:true with MaxFrames=N: the (N+1)th sibling Push evicts the first from Children.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Audit_StartsEmpty()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
