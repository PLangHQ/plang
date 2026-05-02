using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class CallStackTreeTests
{
    [Test]
    public async Task Push_SetsCurrentToNewCall()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        await Assert.That(stack.Current).IsEqualTo(call);
    }

    [Test]
    public async Task Push_NestedPush_SetsCallerToOuter()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("A"));
        await using var inner = stack.Push(MakeAction("B"));
        await Assert.That(inner.Caller).IsEqualTo(outer);
    }

    [Test]
    public async Task Push_AppendsCallToCallerChildren()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("A"));
        await using var inner = stack.Push(MakeAction("B"));
        await Assert.That(outer.Children.Contains(inner)).IsTrue();
    }

    [Test]
    public async Task Pop_RestoresCurrentToCaller()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("A"));
        await using (var inner = stack.Push(MakeAction("B")))
        {
            await Assert.That(stack.Current).IsEqualTo(inner);
        }
        await Assert.That(stack.Current).IsEqualTo(outer);
    }

    [Test]
    public async Task Pop_RemovesFromCallerChildren_WhenHistoryFalse()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction("A"));
        var snapshotInner = (object?)null;
        await using (var inner = stack.Push(MakeAction("B")))
        {
            snapshotInner = inner;
            await Assert.That(outer.Children.Count).IsEqualTo(1);
        }
        await Assert.That(outer.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Pop_RetainsInCallerChildren_WhenHistoryTrue()
    {
        var stack = new CallStack { Flags = CallStackFlags.Default with { History = true } };
        await using var outer = stack.Push(MakeAction("A"));
        await using (var inner = stack.Push(MakeAction("B")))
        {
            await Assert.That(outer.Children.Count).IsEqualTo(1);
        }
        await Assert.That(outer.Children.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Root_IsFirstPushedCall()
    {
        var stack = new CallStack();
        await using var first = stack.Push(MakeAction("A"));
        await using var second = stack.Push(MakeAction("B"));
        await Assert.That(stack.Root).IsEqualTo(first);
    }

    [Test]
    public async Task Root_NullBeforeAnyPush()
    {
        var stack = new CallStack();
        await Assert.That(stack.Root).IsNull();
    }

    [Test]
    public async Task MaxFrames_FifoEvictsOldestSibling_WhenHistoryTrue()
    {
        var stack = new CallStack
        {
            Flags = CallStackFlags.Default with { History = true, MaxFrames = 2 }
        };
        await using var outer = stack.Push(MakeAction("Outer"));

        // Push and pop three siblings; with history retention, all three start as Children
        // but the FIFO cap evicts the oldest after the third Push.
        await using (stack.Push(MakeAction("A"))) { }
        await using (stack.Push(MakeAction("B"))) { }
        await using (stack.Push(MakeAction("C"))) { }

        await Assert.That(outer.Children.Count).IsEqualTo(2);
        // First child evicted; only the two newest remain.
        await Assert.That(outer.Children[0].Action.Step!.Goal!.Name).IsEqualTo("B");
        await Assert.That(outer.Children[1].Action.Step!.Goal!.Name).IsEqualTo("C");
    }

    [Test]
    public async Task Audit_StartsEmpty()
    {
        var stack = new CallStack();
        await Assert.That(stack.Audit.Count).IsEqualTo(0);
    }
}
