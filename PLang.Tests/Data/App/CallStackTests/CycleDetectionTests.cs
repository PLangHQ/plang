using app.error;
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
        // Recursion is allowed — a goal that calls itself forever (directly or A → B → A) is
        // stopped by the depth limit alone: each call is born one deeper than its caller.
        var stack = new CallStack { MaxDepth = 5 };
        var calls = new List<global::app.callstack.call.@this>();
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
    public async Task EnteringAGoalAlreadyOnTheChain_Runs_RecursionIsAllowed()
    {
        await using var app = TestApp.Create("/test");
        var context = app.User.Context;
        // A → B → A: a goal may call itself through others; only the depth limit stops it
        await using var a = context.CallStack.Push(MakeAction("A"));
        await using var b = context.CallStack.Push(MakeAction("B"));
        var goalA = Make.Goal("A", "/A.goal", Make.Step("write out \"x\"", Make.Action("output", "write", ("Data", "x"))));

        var entered = await goalA.Run(context);

        await Assert.That(entered.Error?.Key).IsNotEqualTo("CallStackOverflow");
    }

    [Test]
    public async Task EnteringASubGoalOfTheSameFile_IsNoCycle()
    {
        await using var app = TestApp.Create("/test");
        var context = app.User.Context;
        // Start calls Compile: the file's sub-goals share its .pr — a goal is its .pr and its name
        await using var start = context.CallStack.Push(MakeAction("Start"));
        var compile = Make.Goal("Compile", "/Start.goal", Make.Step("write out \"x\"", Make.Action("output", "write", ("Data", "x"))));

        var entered = await compile.Run(context);

        await Assert.That(entered.Error?.Key).IsNotEqualTo("CallStackOverflow");
    }

    [Test]
    public async Task AHeldActionWrittenInAGoalOnTheChain_Runs()
    {
        // Build → EmitBuildEvent → the builder channel's call, which was written in Build: running it
        // does not enter Build (only a goal's entry does), so it is no cycle.
        var stack = new CallStack();
        await using var build = stack.Push(MakeAction("Build"));
        await using var emit = stack.Push(MakeAction("EmitBuildEvent"));

        await using var held = stack.Push(MakeAction("Build"));

        await Assert.That(held).IsNotNull();
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
