using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class AsyncLocalForkTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task ParallelBranches_DoNotPollute_EachOthersCurrent()
    {
        var stack = new CallStack(TestCallStack.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));

        var branchACurrent = (object?)null;
        var branchBCurrent = (object?)null;

        async Task BranchA()
        {
            await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
            await Task.Yield();
            branchACurrent = stack.Current;
        }
        async Task BranchB()
        {
            await using var b = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
            await Task.Yield();
            branchBCurrent = stack.Current;
        }

        await Task.WhenAll(BranchA(), BranchB());

        // Each branch saw its own Push as Current — not the other's.
        await Assert.That(branchACurrent).IsNotEqualTo(branchBCurrent);
        // After both branches finish, we're back to outer in the calling context.
        await Assert.That(stack.Current).IsEqualTo(outer);
    }

    [Test]
    public async Task ParallelBranches_ShareSameCaller()
    {
        var stack = new CallStack(TestCallStack.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));

        var aCaller = (object?)null;
        var bCaller = (object?)null;

        async Task BranchA() { await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A")); aCaller = a.Caller; await Task.Yield(); }
        async Task BranchB() { await using var b = stack.Push(MakeAction(app.actor.list.User.Context, "B")); bCaller = b.Caller; await Task.Yield(); }

        await Task.WhenAll(BranchA(), BranchB());

        await Assert.That(aCaller).IsEqualTo(outer);
        await Assert.That(bCaller).IsEqualTo(outer);
    }

    [Test]
    public async Task ParallelBranches_BothAppearInOuterChildren_HistoryOn()
    {
        var stack = new CallStack(TestCallStack.Settings(new Dictionary<string, object?> { ["history"] = true }));
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));

        async Task BranchA() { await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A")); await Task.Yield(); }
        async Task BranchB() { await using var b = stack.Push(MakeAction(app.actor.list.User.Context, "B")); await Task.Yield(); }

        await Task.WhenAll(BranchA(), BranchB());

        await Assert.That(outer.Children.Count).IsEqualTo(2);
    }

    [Test]
    public async Task AsyncLocal_RestoresOnDispose_InNestedAwait()
    {
        var stack = new CallStack(TestCallStack.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));

        async Task NestedScope()
        {
            await using var inner = stack.Push(MakeAction(app.actor.list.User.Context, "Inner"));
            await Task.Yield();
        }

        await NestedScope();
        await Assert.That(stack.Current).IsEqualTo(outer);
    }

    [Test]
    public async Task AsyncLocal_FlowsIntoTaskRun()
    {
        var stack = new CallStack(TestCallStack.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));

        var seen = await Task.Run(() => stack.Current);
        await Assert.That(seen).IsEqualTo(outer);
    }

    [Test]
    public async Task FreshAsyncContext_HasNullCurrent()
    {
        var stack = new CallStack(TestCallStack.Settings());
        await Task.Yield();
        await Assert.That(stack.Current).IsNull();
    }
}
