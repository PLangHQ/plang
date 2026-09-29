using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class SnapshotChainTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task SnapshotChain_FirstElementIsSelf()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(MakeAction(app.actor.list.User.Context, "Outer"));
        await using var inner = stack.Push(MakeAction(app.actor.list.User.Context, "Inner"));
        var chain = inner.SnapshotChain();
        await Assert.That(chain[0]).IsEqualTo(inner);
    }

    [Test]
    public async Task SnapshotChain_OrderIsLeafToRoot()
    {
        var stack = new CallStack();
        await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        await using var b = stack.Push(MakeAction(app.actor.list.User.Context, "B"));
        await using var c = stack.Push(MakeAction(app.actor.list.User.Context, "C"));
        var chain = c.SnapshotChain();
        await Assert.That(chain.Count).IsEqualTo(3);
        await Assert.That(chain[0]).IsEqualTo(c);
        await Assert.That(chain[1]).IsEqualTo(b);
        await Assert.That(chain[2]).IsEqualTo(a);
    }

    [Test]
    public async Task SnapshotChain_SingleFrame_ReturnsLengthOne()
    {
        var stack = new CallStack();
        await using var only = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        var chain = only.SnapshotChain();
        await Assert.That(chain.Count).IsEqualTo(1);
        await Assert.That(chain[0]).IsEqualTo(only);
    }

    [Test]
    public async Task SnapshotChain_ReturnsStableRefs_NoCopy()
    {
        var stack = new CallStack();
        await using var a = stack.Push(MakeAction(app.actor.list.User.Context, "A"));
        var chain = a.SnapshotChain();
        await Assert.That(ReferenceEquals(chain[0], a)).IsTrue();
    }

    [Test]
    public async Task SnapshotChain_DoesNotWalkCause()
    {
        var stack = new CallStack();
        await using var goalCall = stack.Push(MakeAction(app.actor.list.User.Context, "Goal"));
        var errored = stack.Push(MakeAction(app.actor.list.User.Context, "Errored"));
        await errored.DisposeAsync();
        await using var recovery = stack.Push(MakeAction(app.actor.list.User.Context, "Recovery"));

        var chain = recovery.SnapshotChain();
        await Assert.That(chain.Contains(errored)).IsFalse();
    }
}
