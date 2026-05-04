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
    public async Task Push_DirectGoalRecursion_TerminatesAtMaxDepth()
    {
        // Direct PLang recursion (goal A's only action is goal.call A) doesn't cross a
        // goal boundary — every frame's Step.Goal.PrPath is the same. ContainsGoal can't
        // distinguish "next action in goal A" from "re-entry into goal A," so direct
        // recursion is caught by MaxDepth, not ContainsGoal. Indirect cycles (A→B→A) DO
        // cross boundaries and are caught by ContainsGoal at Push time — see
        // Push_IndirectGoalCycle_Throws for that path.
        var stack = new CallStack { MaxDepth = 5 };
        var calls = new List<global::App.CallStack.Call.@this>();
        CallStackOverflowException? caught = null;
        try
        {
            for (int i = 0; i < 100; i++)
                calls.Add(stack.Push(MakeAction("A")));
        }
        catch (CallStackOverflowException ex) { caught = ex; }
        finally
        {
            for (int i = calls.Count - 1; i >= 0; i--)
                await calls[i].DisposeAsync();
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.MaxDepth).IsEqualTo(5);
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
