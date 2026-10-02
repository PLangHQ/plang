using app.error;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class CycleDetectionTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Push_ExceedsMaxDepth_ThrowsCallStackOverflowException()
    {
        var stack = new Calls(TestCalls.Settings()) { MaxDepth = 3 };
        await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var b = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
        await using var c = stack.Push(MakeAction(app.actor.list.User.Context, "C"));

        await Assert.ThrowsAsync<CallStackOverflowException>(async () =>
        {
            await Task.Run(() => stack.Push(MakeAction(app.actor.list.User.Context, "D")));
        });
    }

    [Test]
    public async Task CallStackOverflowException_IncludesMaxDepth()
    {
        var stack = new Calls(TestCalls.Settings()) { MaxDepth = 2 };
        await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var b = stack.Push(MakeAction(app.actor.list.User.Context, "B"));

        CallStackOverflowException? caught = null;
        try { stack.Push(MakeAction(app.actor.list.User.Context, "C")); }
        catch (CallStackOverflowException ex) { caught = ex; }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.MaxDepth).IsEqualTo(2);
    }

    [Test]
    public async Task Push_DirectGoalRecursion_TerminatesAtMaxDepth()
    {
        // Recursion is allowed — a goal that calls itself forever (directly or A → B → A) is
        // stopped by the depth limit alone: each call is born one deeper than its caller.
        var stack = new Calls(TestCalls.Settings()) { MaxDepth = 5 };
        var calls = new List<global::app.call.@this>();
        CallStackOverflowException? caught = null;
        try
        {
            for (int i = 0; i < 100; i++)
                calls.Add(stack.Push(MakeAction(app.actor.list.User.Context, "A")));
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
        await using var app = new global::app.@this("/test").Testing();
        var context = app.actor.list.User.Context;
        // A → B → A: a goal may call itself through others; only the depth limit stops it
        await using var a = context.call.Push(MakeAction(context, "A"));
        await using var b = context.call.Push(MakeAction(context, "B"));
        var goalA = Make.Goal(context, "A", "/A.goal", Make.Step("write out \"x\"", Make.Action(context, "output", "write", ("Data", "x"))));

        var entered = await goalA.Start(context);

        await Assert.That(entered.Error?.Key).IsNotEqualTo("CallStackOverflow");
    }

    [Test]
    public async Task EnteringASubGoalOfTheSameFile_IsNoCycle()
    {
        await using var app = new global::app.@this("/test").Testing();
        var context = app.actor.list.User.Context;
        // Start calls Compile: the file's sub-goals share its .pr — a goal is its .pr and its name
        await using var start = context.call.Push(MakeAction(context, "Start"));
        var compile = Make.Goal(context, "Compile", "/Start.goal", Make.Step("write out \"x\"", Make.Action(context, "output", "write", ("Data", "x"))));

        var entered = await compile.Start(context);

        await Assert.That(entered.Error?.Key).IsNotEqualTo("CallStackOverflow");
    }

    [Test]
    public async Task AHeldActionWrittenInAGoalOnTheChain_Runs()
    {
        // Build → EmitBuildEvent → the builder channel's call, which was written in Build: running it
        // does not enter Build (only a goal's entry does), so it is no cycle.
        var stack = new Calls(TestCalls.Settings());
        await using var build = stack.Push(MakeAction(app.actor.list.User.Context, "Build"));
        await using var emit = stack.Push(MakeAction(app.actor.list.User.Context, "EmitBuildEvent"));

        await using var held = stack.Push(MakeAction(app.actor.list.User.Context, "Build"));

        await Assert.That(held).IsNotNull();
    }

    [Test]
    public async Task Push_RepeatedSiblingNotInChain_DoesNotThrow()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        var b = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
        await b.DisposeAsync();
        // After B is popped, the live chain is [A]. Pushing C is fine.
        await using var c = stack.Push(MakeAction(app.actor.list.User.Context, "C"));
        await Assert.That(c).IsNotNull();
    }
}
