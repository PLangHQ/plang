using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

// Spec for App.CallStack.Call.@this — the renamed CallFrame, OBP-shaped.
public class CallTests
{
    [Test]
    public async Task Call_HasUniqueId_PerInstance()
    {
        var stack = new CallStack();
        await using var a = stack.Push(MakeAction("A"));
        await using var b = stack.Push(MakeAction("B"));
        await Assert.That(a.Id).IsNotEqualTo(b.Id);
    }

    [Test]
    public async Task Call_Action_IsTheActionPassedToPush()
    {
        var stack = new CallStack();
        var action = MakeAction("X");
        await using var call = stack.Push(action);
        await Assert.That(ReferenceEquals(call.Action, action)).IsTrue();
    }

    [Test]
    public async Task Call_Caller_IsAsyncLocalCurrentAtPushTime()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("Outer"));
        await using var inner = stack.Push(MakeAction("Inner"));
        await Assert.That(inner.Caller).IsEqualTo(outer);
    }

    [Test]
    public async Task Call_Errors_StartsEmpty()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        await Assert.That(call.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Call_Handled_DefaultsToFalse()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        await Assert.That(call.Handled).IsFalse();
    }

    [Test]
    public async Task Call_Children_AlwaysAllocated_NotNull()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        await Assert.That(call.Children).IsNotNull();
    }

    [Test]
    public async Task Call_StartedAt_PopulatedWhenTimingFlagOn()
    {
        var on = new CallStack { Timing = true };
        var off = new CallStack();
        await using var withTiming = on.Push(MakeAction("A"));
        await using var noTiming = off.Push(MakeAction("A"));
        await Assert.That(withTiming.StartedAt).IsNotEqualTo(default(DateTimeOffset));
        await Assert.That(noTiming.StartedAt).IsEqualTo(default(DateTimeOffset));
    }

    [Test]
    public async Task Call_Tags_StartsEmpty()
    {
        // Tags is now always-allocated (Tags.@this owns its lock; lazy alloc would have
        // raced with the writer pattern). No tag written → Count == 0.
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        await Assert.That(call.Tags.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Call_DisposeAsync_PopsItselfFromStack()
    {
        var stack = new CallStack();
        var outerAction = MakeAction("Outer");
        await using (var outer = stack.Push(outerAction))
        {
            var innerAction = MakeAction("Inner");
            await using (var inner = stack.Push(innerAction))
            {
                await Assert.That(stack.Current).IsEqualTo(inner);
            }
            await Assert.That(stack.Current).IsEqualTo(outer);
        }
        await Assert.That(stack.Current).IsNull();
    }
}
