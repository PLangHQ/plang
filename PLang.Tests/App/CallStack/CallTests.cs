namespace PLang.Tests.App.CallStack;

// Spec for App.CallStack.Call.@this — the renamed CallFrame, OBP-shaped.
// Bodies are stubs; coder implements against architect plan v1.
public class CallTests
{
    [Test]
    public async Task Call_HasUniqueId_PerInstance()
    {
        // Two distinct Pushes produce Calls with different Ids.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Action_IsTheActionPassedToPush()
    {
        // call.Action is the OBP ref (not a copy) of the Action passed to Push.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Caller_IsAsyncLocalCurrentAtPushTime()
    {
        // Caller is whatever CallStack._current.Value was at Push time, not a constructor arg.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Cause_NullByDefault()
    {
        // Push without an explicit cause: parameter leaves Cause null.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Errors_StartsEmpty()
    {
        // Errors list initialized empty after construction.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Handled_DefaultsToFalse()
    {
        // Handled flag default false; flipped true only by error.handle.Wrap on recovery success.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Children_AlwaysAllocated_NotNull()
    {
        // Children list is always allocated (history flag controls retention, not allocation).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_StartedAt_PopulatedWhenTimingFlagOn()
    {
        // Flags{Timing=true}: StartedAt is set on Push. Flags{Timing=false}: stays default(DateTimeOffset).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Tags_NullWhenTagsFlagOff()
    {
        // Tags dict is null when Flags.Tags=false and Tag() never called.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_DisposeAsync_PopsItselfFromStack()
    {
        // await using exit restores CallStack.Current to the Caller value before this Push.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
