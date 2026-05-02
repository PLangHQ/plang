namespace PLang.Tests.App.CallStack;

// CallStackFlags is a record struct gating per-property population on Call.@this.
public class CallStackFlagsTests
{
    [Test]
    public async Task CallStackFlags_DefaultIsAllFalse_MaxFrames1000()
    {
        // default(CallStackFlags) has Timing/Diff/DeepDiff/Tags/History all false; MaxFrames default 1000.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackFlags_RecordStruct_EqualityByValue()
    {
        // Two flag values with identical fields compare equal.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackFlags_Timing_GatesStartedAtCompletedAt()
    {
        // Timing=false → StartedAt/CompletedAt remain default; Timing=true → populated.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackFlags_Diff_GatesDiffsCollection()
    {
        // Diff=false → Call.Diffs is null; Diff=true → allocated and subscribes Variables.OnSet.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackFlags_DeepDiff_RequiresDiff()
    {
        // DeepDiff=true with Diff=false is a no-op (no clone capture without diff machinery).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackFlags_Tags_GatesTagsDictAllocation()
    {
        // Tags=false → Tag() is no-op (or allocates only on write?). Verify documented behavior.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackFlags_History_RetainsPoppedChildren()
    {
        // History=true: Pop leaves Call in Caller.Children.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackFlags_MaxFrames_DefaultsTo1000()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
