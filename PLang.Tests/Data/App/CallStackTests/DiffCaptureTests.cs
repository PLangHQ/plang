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
        // Regression guard for the prior OOM: under Diff (no DeepDiff), capturing a
        // non-scalar Before must record a summary STRING, never a clone — that is what
        // stops a tight set-loop from ballooning memory. We assert the invariant
        // DIRECTLY (Before is a summary string) instead of measuring a GC delta: each
        // `await using` disposes the call and releases its Diffs, so retained memory is
        // ~0 whether we summarise or clone — the old memory-delta proxy was both noisy
        // and blind to a cloning regression. Asserting the summary is exact and cheap.
        //
        // Kept small on purpose: every Set of a typed primitive list round-trips through
        // JSON narrowing in Data.Lift (O(elements) with a heavy constant), so the old
        // 100×1MB form ran ~26s — ~40% of the whole C# suite. A handful of small sets
        // proves the same invariant.
        var stack = new CallStack { Flags = Flags.Default with { Diff = true } };
        var vars = new global::app.variable.list.@this();
        vars.Set("big", new List<int>(new int[256]));

        for (int i = 0; i < 10; i++)
        {
            await using var call = stack.Push(MakeAction("A"), vars);
            vars.Set("big", new List<int>(new int[256]));
            var diff = call.Diffs![0];
            await Assert.That(diff.Before is string).IsTrue();
            await Assert.That(((string)diff.Before!).Contains("list")).IsTrue();
        }
    }
}
