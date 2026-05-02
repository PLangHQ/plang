using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class CallStackFlagsTests
{
    [Test]
    public async Task CallStackFlags_DefaultIsAllFalse_MaxFrames1000()
    {
        var d = CallStackFlags.Default;
        await Assert.That(d.Timing).IsFalse();
        await Assert.That(d.Diff).IsFalse();
        await Assert.That(d.DeepDiff).IsFalse();
        await Assert.That(d.Tags).IsFalse();
        await Assert.That(d.History).IsFalse();
        await Assert.That(d.MaxFrames).IsEqualTo(1000);
    }

    [Test]
    public async Task CallStackFlags_RecordStruct_EqualityByValue()
    {
        var a = new CallStackFlags(true, false, false, true, false, 500);
        var b = new CallStackFlags(true, false, false, true, false, 500);
        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task CallStackFlags_Timing_GatesStartedAtCompletedAt()
    {
        var on = new CallStack { Flags = CallStackFlags.Default with { Timing = true } };
        var off = new CallStack();

        await using (var withTiming = on.Push(MakeAction("A")))
        {
            await Assert.That(withTiming.StartedAt).IsNotEqualTo(default(DateTimeOffset));
        }
        await using (var noTiming = off.Push(MakeAction("A")))
        {
            await Assert.That(noTiming.StartedAt).IsEqualTo(default(DateTimeOffset));
        }
    }

    [Test]
    public async Task CallStackFlags_Diff_GatesDiffsCollection()
    {
        var on = new CallStack { Flags = CallStackFlags.Default with { Diff = true } };
        var off = new CallStack();
        var vars = new global::App.Variables.@this();

        await using var withDiff = on.Push(MakeAction("A"), vars);
        await using var noDiff = off.Push(MakeAction("A"), vars);
        await Assert.That(withDiff.Diffs).IsNotNull();
        await Assert.That(noDiff.Diffs).IsNull();
    }

    [Test]
    public async Task CallStackFlags_DeepDiff_RequiresDiff()
    {
        // DeepDiff with Diff off → no diff machinery at all (no clones, no list).
        var stack = new CallStack { Flags = CallStackFlags.Default with { DeepDiff = true } };
        var vars = new global::App.Variables.@this();
        await using var call = stack.Push(MakeAction("A"), vars);
        await Assert.That(call.Diffs).IsNull();
    }

    [Test]
    public async Task CallStackFlags_Tags_FlagOff_TagWriteStillAllocatesDictOnDemand()
    {
        // Spec: tags flag is renderer-meaningful; the writer always succeeds — Tag()
        // lazy-allocates regardless of the flag.
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        call.Tag("k", "v");
        await Assert.That(call.Tags).IsNotNull();
        await Assert.That(call.Tags!["k"]).IsEqualTo("v");
    }

    [Test]
    public async Task CallStackFlags_History_RetainsPoppedChildren()
    {
        var stack = new CallStack { Flags = CallStackFlags.Default with { History = true } };
        await using var outer = stack.Push(MakeAction("A"));
        var inner = stack.Push(MakeAction("B"));
        await inner.DisposeAsync();
        await Assert.That(outer.Children.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CallStackFlags_MaxFrames_DefaultsTo1000()
    {
        await Assert.That(CallStackFlags.Default.MaxFrames).IsEqualTo(1000);
    }
}
