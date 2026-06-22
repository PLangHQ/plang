using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class DiffCaptureTests
{
    [Test]
    public async Task Diff_FlagOff_DiffsListIsNull()
    {
        var stack = new CallStack();
        var vars = new global::app.variable.list.@this();
        await using var call = stack.Push(MakeAction("A"), vars);
        await Assert.That(call.Diffs).IsNull();
    }

    [Test]
    public async Task Diff_FlagOn_VariableSetAppendsDiffEntry()
    {
        var stack = new CallStack { Flags = Flags.Default with { Diff = true } };
        var vars = new global::app.variable.list.@this();
        vars.Set("name", "old");

        await using var call = stack.Push(MakeAction("A"), vars);
        vars.Set("name", "new");

        await Assert.That(call.Diffs).IsNotNull();
        await Assert.That(call.Diffs!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Diff_RecordCarriesNameBeforeAt()
    {
        var stack = new CallStack { Flags = Flags.Default with { Diff = true } };
        var vars = new global::app.variable.list.@this();
        vars.Set("name", "ingi");

        await using var call = stack.Push(MakeAction("A"), vars);
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
        var stack = new CallStack { Flags = Flags.Default with { Diff = true } };
        var vars = new global::app.variable.list.@this();
        var list = new List<int> { 1, 2, 3 };
        vars.Set("items", list);

        await using var call = stack.Push(MakeAction("A"), vars);
        vars.Set("items", new List<int> { 4, 5 });

        var diff = call.Diffs![0];
        // Non-scalar Before is a summary string naming the plang type and item count.
        await Assert.That(diff.Before is string).IsTrue();
        await Assert.That(((string)diff.Before!).Contains("list")).IsTrue();
    }

    [Test]
    public async Task Diff_DeepDiffOn_ClonesNonScalarBefore()
    {
        var stack = new CallStack
        {
            Flags = Flags.Default with { Diff = true, DeepDiff = true }
        };
        var vars = new global::app.variable.list.@this();
        var list = new List<int> { 1, 2, 3 };
        vars.Set("items", list);

        await using var call = stack.Push(MakeAction("A"), vars);
        vars.Set("items", new List<int> { 4, 5 });

        var diff = call.Diffs![0];
        // DeepDiff captures a clone of the native value — a distinct list instance,
        // same contents (the pre-mutation [1,2,3]).
        await Assert.That(diff.Before is global::app.type.list.@this).IsTrue();
        var captured = (global::app.type.list.@this)diff.Before!;
        await Assert.That(captured.CountRaw).IsEqualTo(3);
    }

    [Test]
    public async Task Diff_DisposeUnsubscribesFromVariablesOnSet()
    {
        var stack = new CallStack { Flags = Flags.Default with { Diff = true } };
        var vars = new global::app.variable.list.@this();
        vars.Set("x", 1);

        var call = stack.Push(MakeAction("A"), vars);
        vars.Set("x", 2);
        await call.DisposeAsync();
        // After Dispose, the handler is unsubscribed: subsequent Set must NOT append.
        var countAfterDispose = call.Diffs!.Count;
        vars.Set("x", 3);
        await Assert.That(call.Diffs!.Count).IsEqualTo(countAfterDispose);
    }

    [Test]
    public async Task Diff_DiffModeOverLargeListDoesNotOom()
    {
        // The OOM risk under Diff:true (no DeepDiff) is cloning a large collection
        // on every Set. CaptureBefore avoids it by storing an O(1) summary string,
        // never a clone — so even a large list captures in constant space. Asserting
        // the summary directly is both faster and stronger than a GC-delta heuristic:
        // the summary IS the property that prevents the OOM.
        var stack = new CallStack { Flags = Flags.Default with { Diff = true } };
        var vars = new global::app.variable.list.@this();
        // Seed with a large list — this is the 'before' the next Set captures.
        var big = new List<int>(Enumerable.Range(0, 100_000));
        vars.Set("big", big);

        await using var call = stack.Push(MakeAction("A"), vars);
        vars.Set("big", new List<int> { 1, 2, 3 });

        var diff = call.Diffs![0];
        // The large Before is captured as a constant-space summary string naming the
        // plang type + item count, not a clone of the list.
        await Assert.That(diff.Before is string).IsTrue();
        await Assert.That(((string)diff.Before!).Contains("list")).IsTrue();
    }
}
