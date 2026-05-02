using global::App.Errors;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class CycleDetectionTests
{
    [Test]
    public async Task Push_ExceedsMaxDepth_ThrowsCallStackOverflowException()
    {
        var stack = new CallStack { MaxDepth = 3 };
        await using var a = stack.Push(MakeAction("A"));
        await using var b = stack.Push(MakeAction("B"));
        await using var c = stack.Push(MakeAction("C"));

        await Assert.ThrowsAsync<CallStackOverflowException>(async () =>
        {
            await Task.Run(() => stack.Push(MakeAction("D")));
        });
    }

    [Test]
    public async Task CallStackOverflowException_IncludesMaxDepth()
    {
        var stack = new CallStack { MaxDepth = 2 };
        await using var a = stack.Push(MakeAction("A"));
        await using var b = stack.Push(MakeAction("B"));

        CallStackOverflowException? caught = null;
        try { stack.Push(MakeAction("C")); }
        catch (CallStackOverflowException ex) { caught = ex; }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.MaxDepth).IsEqualTo(2);
    }

    [Test]
    public async Task Push_DirectGoalCycle_ThrowsViaContainsGoal()
    {
        var stack = new CallStack();
        await using var first = stack.Push(MakeAction("A"));
        await Assert.ThrowsAsync<CallStackOverflowException>(async () =>
        {
            await Task.Run(() => stack.Push(MakeAction("A")));
        });
    }

    [Test]
    public async Task Push_IndirectGoalCycle_Throws()
    {
        var stack = new CallStack();
        await using var a = stack.Push(MakeAction("A"));
        await using var b = stack.Push(MakeAction("B"));
        // A → B → A: pushing A again is a cycle in the live caller chain.
        await Assert.ThrowsAsync<CallStackOverflowException>(async () =>
        {
            await Task.Run(() => stack.Push(MakeAction("A")));
        });
    }

    [Test]
    public async Task Push_RepeatedSiblingNotInChain_DoesNotThrow()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("A"));
        var b = stack.Push(MakeAction("B"));
        await b.DisposeAsync();
        // After B is popped, the live chain is [A]. Pushing C is fine.
        await using var c = stack.Push(MakeAction("C"));
        await Assert.That(c).IsNotNull();
    }
}
