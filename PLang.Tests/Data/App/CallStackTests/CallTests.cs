using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

// Spec for App.CallStack.Call.@this — the renamed CallFrame, OBP-shaped.
public class CallTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Call_HasUniqueId_PerInstance()
    {
        var stack = new CallStack();
        await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var b = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
        await Assert.That(a.Id).IsNotEqualTo(b.Id);
    }

    [Test]
    public async Task Call_Action_IsTheActionPassedToPush()
    {
        var stack = new CallStack();
        var action = MakeAction(app.actor.list.User.Context, "X");
        await using var call = stack.Push(action);
        await Assert.That(ReferenceEquals(call.Action, action)).IsTrue();
        await Assert.That(ReferenceEquals(call.Step, action.Step)).IsTrue();
    }

    [Test]
    public async Task Call_Caller_IsAsyncLocalCurrentAtPushTime()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));
        await using var inner = stack.Push(MakeAction(app.actor.list.User.Context, "Inner"));
        await Assert.That(inner.Caller).IsEqualTo(outer);
    }

    [Test]
    public async Task Call_Errors_StartsEmpty()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await Assert.That(call.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Call_Handled_DefaultsToFalse()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await Assert.That(call.Handled).IsFalse();
    }

    [Test]
    public async Task Call_Children_AlwaysAllocated_NotNull()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await Assert.That(call.Children).IsNotNull();
    }

    [Test]
    public async Task Call_StartedAt_PopulatedWhenTimingFlagOn()
    {
        var on = new CallStack { Setting = new() { Timing = true } };
        var off = new CallStack();
        await using var withTiming = on.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var noTiming = off.Push(MakeAction(app.actor.list.User.Context, "A"));
        await Assert.That(withTiming.StartedAt).IsNotEqualTo(default(DateTimeOffset));
        await Assert.That(noTiming.StartedAt).IsEqualTo(default(DateTimeOffset));
    }

    [Test]
    public async Task Call_Tags_StartsEmpty()
    {
        // Tags is always allocated (a lazy alloc would race the writer). No tag written → empty.
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await Assert.That(await call.Tags.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task Call_DisposeAsync_PopsItselfFromStack()
    {
        var stack = new CallStack();
        var outerAction = MakeAction(app.actor.list.User.Context, "Outer");
        await using (var outer = stack.Push(outerAction))
        {
            var innerAction = MakeAction(app.actor.list.User.Context, "Inner");
            await using (var inner = stack.Push(innerAction))
            {
                await Assert.That(stack.Current).IsEqualTo(inner);
            }
            await Assert.That(stack.Current).IsEqualTo(outer);
        }
        await Assert.That(stack.Current).IsNull();
    }
}
