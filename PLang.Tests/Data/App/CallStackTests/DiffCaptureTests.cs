using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class DiffCaptureTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/DiffCaptureTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // The User actor's call stack with these diff options written through its context's settings — the stack a store
    // of the User's context records its changes on.
    private CallStack Stack(bool deep = false)
    {
        var set = _app.actor.list.User.Context.Setting.Set(new global::app.callstack.setting.@this().Path,
            new Dictionary<string, object?> { ["diff"] = new Dictionary<string, object?> { ["enabled"] = true, ["deep"] = deep } });
        if (!set.Success) throw new System.InvalidOperationException(set.Error!.Message);
        return _app.actor.list.User.CallStack;
    }

    [Test]
    public async Task Diff_FlagOff_DiffsListIsNull()
    {
        var stack = new CallStack(TestCallStack.Settings());
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        await Assert.That(call.Diffs).IsNull();
    }

    [Test]
    public async Task Diff_FlagOn_VariableSetAppendsDiffEntry()
    {
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        vars.Set("name", "old");

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        vars.Set("name", "new");

        await Assert.That(call.Diffs).IsNotNull();
        await Assert.That(call.Diffs!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Diff_RecordCarriesNameBeforeAt()
    {
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        vars.Set("name", "ingi");

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        var before = DateTimeOffset.UtcNow.AddMilliseconds(-10);
        vars.Set("name", "olafur");

        var diff = call.Diffs![0];
        await Assert.That(diff.Name).IsEqualTo("name");
        await Assert.That(diff.Before?.ToString()).IsEqualTo("ingi");
        await Assert.That(diff.At).IsGreaterThanOrEqualTo(before);
    }

    [Test]
    public async Task Diff_ScalarOnlyByDefault_NonScalarRendersAsSummary()
    {
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        var list = new List<int> { 1, 2, 3 };
        vars.Set("items", list);

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        vars.Set("items", new List<int> { 4, 5 });

        var diff = call.Diffs![0];
        // Non-scalar Before is a summary string naming the plang type and item count.
        await Assert.That(diff.Before is string).IsTrue();
        await Assert.That(((string)diff.Before!).Contains("list")).IsTrue();
    }

    [Test]
    public async Task Diff_DeepDiffOn_ClonesNonScalarBefore()
    {
        var stack = Stack(deep: true);
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        var list = new List<int> { 1, 2, 3 };
        vars.Set("items", list);

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        vars.Set("items", new List<int> { 4, 5 });

        var diff = call.Diffs![0];
        // diff.deep captures a clone of the native value — a distinct list instance,
        // same contents (the pre-mutation [1,2,3]).
        await Assert.That(diff.Before is global::app.type.item.list.@this).IsTrue();
        var captured = (global::app.type.item.list.@this)diff.Before!;
        await Assert.That(captured.CountRaw).IsEqualTo(3);
    }

    [Test]
    public async Task Diff_DisposeUnsubscribesFromVariablesOnSet()
    {
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        vars.Set("x", 1);

        var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        vars.Set("x", 2);
        await call.DisposeAsync();
        // After Dispose, the handler is unsubscribed: subsequent Set must NOT append.
        var countAfterDispose = call.Diffs!.Count;
        vars.Set("x", 3);
        await Assert.That(call.Diffs!.Count).IsEqualTo(countAfterDispose);
    }

    [Test]
    public async Task Diff_ASetInACallOverlay_IsRecordedToo()
    {
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        await using (vars.Calls.Push(new[] { new Data("greeting", "hello", context: _app.actor.list.User.Context) }))
            await vars.Set("greeting", "bye");

        await Assert.That(call.Diffs!.Count).IsEqualTo(1);
        await Assert.That(call.Diffs[0].Before?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task Diff_EveryOpenFrameOnTheStore_GetsTheChange_AndAnEndedOneNoMore()
    {
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        await vars.Set("x", 1);

        await using var outer = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        var inner = stack.Push(MakeAction(_app.actor.list.User.Context, "B"), vars);
        await vars.Set("x", 2);
        await inner.DisposeAsync();
        await vars.Set("x", 3);

        await Assert.That(outer.Diffs!.Count).IsEqualTo(2);
        await Assert.That(inner.Diffs!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Diff_AFrameOnAnotherStore_GetsNothing()
    {
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        var other = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), other);
        await vars.Set("x", 1);

        await Assert.That(call.Diffs!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Diff_TheFrameHistory_RollsASnapshotBack()
    {
        var stack = Stack();
        var vars = _app.actor.list.User.Context.Variable;
        await vars.Set("x", 1);
        await vars.Set("y", "kept");

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        var error = new global::app.error.ServiceError("boom", "TestErr", 400);
        await vars.Set("x", 2);
        await vars.Set("z", "new");

        var projection = vars.SnapshotAt(error);
        await Assert.That((await (await projection.Get("x")).Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await (await projection.Get("y")).Value())?.ToString()).IsEqualTo("kept");
    }

    [Test]
    public async Task Diff_DiffModeOverLargeListDoesNotOom()
    {
        // The OOM risk under Diff:true (no diff.deep) is cloning a large collection
        // on every Set. CaptureBefore avoids it by storing an O(1) summary string,
        // never a clone — so even a large list captures in constant space. Asserting
        // the summary directly is both faster and stronger than a GC-delta heuristic:
        // the summary IS the property that prevents the OOM.
        var stack = Stack();
        var vars = new global::app.type.item.variable.list.@this(_app.actor.list.User.Context);
        // Seed with a large list — this is the 'before' the next Set captures.
        var big = new List<int>(Enumerable.Range(0, 100_000));
        vars.Set("big", big);

        await using var call = stack.Push(MakeAction(_app.actor.list.User.Context, "A"), vars);
        vars.Set("big", new List<int> { 1, 2, 3 });

        var diff = call.Diffs![0];
        // The large Before is captured as a constant-space summary string naming the
        // plang type + item count, not a clone of the list.
        await Assert.That(diff.Before is string).IsTrue();
        await Assert.That(((string)diff.Before!).Contains("list")).IsTrue();
    }
}
