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
        // scalar-only default should mitigate the prior OOM scenario.
        // 100 iterations × 1MB list under Diff:true (no DeepDiff). GC delta stays low
        // because Before captures a summary string, not a clone.
        var stack = new CallStack { Flags = Flags.Default with { Diff = true } };
        var vars = new global::app.variable.list.@this();
        // Seed with a 1MB-ish list.
        vars.Set("big", new List<byte>(new byte[1024 * 1024]));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memBefore = GC.GetTotalMemory(false);

        for (int i = 0; i < 100; i++)
        {
            await using var call = stack.Push(MakeAction("A"), vars);
            // Replace the big list — capture should be a summary string, not a clone.
            vars.Set("big", new List<byte>(new byte[1024 * 1024]));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memAfter = GC.GetTotalMemory(false);
        var deltaMb = (memAfter - memBefore) / (1024 * 1024);

        // 100 cloned 1MB lists would be ~100MB. Scalar default keeps growth far below that.
        await Assert.That(deltaMb).IsLessThan(50);
    }
}
