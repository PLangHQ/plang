using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class CallStackTreeTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task Push_SetsCurrentToNewCall()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var call = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await Assert.That(stack.Current).IsEqualTo(call);
    }

    [Test]
    public async Task Push_NestedPush_SetsCallerToOuter()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var inner = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
        await Assert.That(inner.Caller).IsEqualTo(outer);
    }

    [Test]
    public async Task Push_AppendsCallToCallerChildren()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var inner = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
        await Assert.That(outer.Children.Contains(inner)).IsTrue();
    }

    [Test]
    public async Task Pop_RestoresCurrentToCaller()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using (var inner = stack.Push(MakeAction(app.actor.list.User.Context, "B")))
        {
            await Assert.That(stack.Current).IsEqualTo(inner);
        }
        await Assert.That(stack.Current).IsEqualTo(outer);
    }

    [Test]
    public async Task Pop_RemovesFromCallerChildren_WhenHistoryFalse()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        var snapshotInner = (object?)null;
        await using (var inner = stack.Push(MakeAction(app.actor.list.User.Context, "B")))
        {
            snapshotInner = inner;
            await Assert.That(outer.Children.Count).IsEqualTo(1);
        }
        await Assert.That(outer.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Pop_RetainsInCallerChildren_WhenHistoryTrue()
    {
        var stack = new Calls(TestCalls.Settings(new Dictionary<string, object?> { ["history"] = true }));
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using (var inner = stack.Push(MakeAction(app.actor.list.User.Context, "B")))
        {
            await Assert.That(outer.Children.Count).IsEqualTo(1);
        }
        await Assert.That(outer.Children.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Root_IsFirstPushedCall()
    {
        var stack = new Calls(TestCalls.Settings());
        await using var first = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var second = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
        await Assert.That(stack.Root).IsEqualTo(first);
    }

    [Test]
    public async Task Root_NullBeforeAnyPush()
    {
        var stack = new Calls(TestCalls.Settings());
        await Assert.That(stack.Root).IsNull();
    }

    [Test]
    public async Task MaxFrames_FifoEvictsOldestSibling_WhenHistoryTrue()
    {
        var stack = new Calls(TestCalls.Settings(new Dictionary<string, object?>
        {
            ["history"] = true, ["frame"] = new Dictionary<string, object?> { ["max"] = 2 },
        }));
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));

        // Push and pop three siblings; with history retention, all three start as Children
        // but the FIFO cap evicts the oldest after the third Push.
        await using (stack.Push(MakeAction(app.actor.list.User.Context, "A"))) { }
        await using (stack.Push(MakeAction(app.actor.list.User.Context, "B"))) { }
        await using (stack.Push(MakeAction(app.actor.list.User.Context, "C"))) { }

        await Assert.That(outer.Children.Count).IsEqualTo(2);
        // First child evicted; only the two newest remain.
        await Assert.That(outer.Children[0].Goal!.Name).IsEqualTo("B");
        await Assert.That(outer.Children[1].Goal!.Name).IsEqualTo("C");
    }

    [Test]
    public async Task Audit_StartsEmpty()
    {
        var stack = new Calls(TestCalls.Settings());
        await Assert.That(stack.Audit.Count).IsEqualTo(0);
    }
}
