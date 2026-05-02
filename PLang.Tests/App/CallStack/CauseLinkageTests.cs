namespace PLang.Tests.App.CallStack;

// Cause models in-process async causality. Distinct from Caller (sync parent).
public class CauseLinkageTests
{
    [Test]
    public async Task Push_WithCause_SetsCauseField()
    {
        // stack.Push(action, cause: erroredCall) → call.Cause == erroredCall.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_WithoutCause_LeavesCauseNull()
    {
        // Normal Push leaves Cause null even if a Caller chain is present.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Cause_IsIndependentOfCaller()
    {
        // Recovery scenario: Caller is the goal-level Call, Cause is the errored sibling.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Cause_KeepsTargetReachable_AfterDispose()
    {
        // Errored Call popped; recovery Call holding Cause keeps the errored Call reachable
        // (no GC) for the recovery's lifetime.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Cause_SnapshotChain_DoesNotIncludeCause()
    {
        // SnapshotChain walks Caller only — Cause-linked siblings are not in the chain.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Cause_IsCallReferenceOnly_NotString()
    {
        // Type contract: Cause is Call.@this? — no string variant.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
