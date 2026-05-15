using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class CauseLinkageTests
{
    [Test]
    public async Task Push_WithCause_SetsCauseField()
    {
        var stack = new CallStack();
        await using var goalCall = stack.Push(MakeAction("Goal"));
        await using var errored = stack.Push(MakeAction("Errored"));
        // Simulate recovery: pop errored, then push recovery body action under goal with cause=errored.
        // (We can't actually pop while under `await using`, so instead push a new sibling under goal
        // by going up through the AsyncLocal flow.)
        var stack2 = new CallStack();
        await using var g = stack2.Push(MakeAction("G"));
        await using var failing = stack2.Push(MakeAction("R"));
        // Use the failing call as cause for a hypothetical recovery push under g.
        // We push directly with cause to confirm the field wires.
        var stack3 = new CallStack();
        await using var top = stack3.Push(MakeAction("Top"));
        await using var recoveryDispatch = stack3.Push(MakeAction("Recovery"), cause: failing);
        await Assert.That(recoveryDispatch.Cause).IsEqualTo(failing);
    }

    [Test]
    public async Task Push_WithoutCause_LeavesCauseNull()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("A"));
        await using var inner = stack.Push(MakeAction("B"));
        await Assert.That(inner.Cause).IsNull();
    }

    [Test]
    public async Task Cause_IsIndependentOfCaller()
    {
        var stack = new CallStack();
        await using var goalCall = stack.Push(MakeAction("Goal"));
        // Errored call lives under the same goal — it's the sync sibling that failed.
        var erroredHandle = stack.Push(MakeAction("Errored"));
        // Pop errored. After this `await using var`, _current rewinds to goalCall.
        await erroredHandle.DisposeAsync();
        // Now push a recovery body action: Caller is goal, Cause is the errored sibling.
        await using var recovery = stack.Push(MakeAction("Recovery"), cause: erroredHandle);
        await Assert.That(recovery.Caller).IsEqualTo(goalCall);
        await Assert.That(recovery.Cause).IsEqualTo(erroredHandle);
        await Assert.That(recovery.Caller).IsNotEqualTo(recovery.Cause);
    }

    [Test]
    public async Task Cause_KeepsTargetReachable_AfterDispose()
    {
        var stack = new CallStack();
        await using var goalCall = stack.Push(MakeAction("Goal"));
        var erroredHandle = stack.Push(MakeAction("Errored"));
        await erroredHandle.DisposeAsync();
        await using var recovery = stack.Push(MakeAction("Recovery"), cause: erroredHandle);

        // After the errored Call's Dispose, the recovery's Cause still points to it.
        await Assert.That(recovery.Cause).IsNotNull();
        await Assert.That(recovery.Cause!.Action.Step!.Goal!.Name).IsEqualTo("Errored");
    }

    [Test]
    public async Task Cause_SnapshotChain_DoesNotIncludeCause()
    {
        var stack = new CallStack();
        await using var goalCall = stack.Push(MakeAction("Goal"));
        var erroredHandle = stack.Push(MakeAction("Errored"));
        await erroredHandle.DisposeAsync();
        await using var recovery = stack.Push(MakeAction("Recovery"), cause: erroredHandle);

        var chain = recovery.SnapshotChain();
        // Chain walks Caller only — [recovery, goalCall]. Cause-linked errored is NOT in it.
        await Assert.That(chain.Contains(erroredHandle)).IsFalse();
        await Assert.That(chain[0]).IsEqualTo(recovery);
        await Assert.That(chain[1]).IsEqualTo(goalCall);
    }

    [Test]
    public async Task Cause_IsCallReferenceOnly_NotString()
    {
        // Compile-time contract: the property type is Call.@this?, not string. This test
        // documents the type at the source level — `IsTypeOf` runtime check is redundant.
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        global::app.CallStack.Call.@this? cause = call.Cause;
        await Assert.That(cause).IsNull();
    }
}
