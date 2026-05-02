namespace PLang.Tests.App.CallStack;

// AsyncLocal<Call?> _current is the only shared mutable state; it must fork
// cleanly across parallel branches so the tree is honest about parallel execution.
public class AsyncLocalForkTests
{
    [Test]
    public async Task ParallelBranches_DoNotPollute_EachOthersCurrent()
    {
        // Task.WhenAll(A, B): branch A's Current never observes branch B's Pushes.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ParallelBranches_ShareSameCaller()
    {
        // Both branches' first Pushes have Caller == outer (the value AsyncLocal had at fork time).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ParallelBranches_BothAppearInOuterChildren_HistoryOn()
    {
        // history:true: outer.Children contains both branch root Calls after WhenAll completes.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AsyncLocal_RestoresOnDispose_InNestedAwait()
    {
        // After `await using` exits inside an async helper, Current is the outer value again.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AsyncLocal_FlowsIntoTaskRun()
    {
        // Task.Run inside a Push body sees the same Current ref (ExecutionContext flow).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FreshAsyncContext_HasNullCurrent()
    {
        // A new CallStack instance with no Push has Current == null.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
