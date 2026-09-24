
namespace PLang.Tests.App.CollectionsAreData;

// Stage 2 — `set` rebinds, not mutates. The two raw branches of Variables.Set
// (frame-overlay, underlying-dict) must mint a new Data on a same-type set —
// matching the Data-value branch that already rebinds. Pin both in isolation so the
// alias bug doesn't reappear inside channel-fire or parallel-foreach flows.
public class Stage2_SetRebindTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-setrebind-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Set_RawFrameOverlayBranch_RebindsNotMutates()
    {
        // The :199 branch path (frame-overlay raw branch). A same-type Set with a raw value
        // must mint a new Data and replace the binding — the previously-bound Data instance
        // (e.g. one held by a list) must compare reference-unequal to the new binding (M).
        await using var app = NewApp();
        var vars = app.User.Context.Variable;
        await using var frame = vars.Calls.Push(null);

        vars.Set("x", "a");
        var dataA = await vars.Get("x");
        vars.Set("x", "b");
        var dataB = await vars.Get("x");

        await Assert.That(ReferenceEquals(dataA, dataB)).IsFalse();
        await Assert.That(dataA.Peek()?.ToString()).IsEqualTo("a"); // old binding untouched
        await Assert.That(dataB.Peek()?.ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Set_RawUnderlyingDictBranch_RebindsNotMutates()
    {
        // The :227 branch path (underlying-dict raw branch). Same shape as the frame-overlay
        // test, but exercising the alternate raw-branch arm so a future split doesn't leave
        // one mutating in place.
        await using var app = NewApp();
        var vars = app.User.Context.Variable;

        vars.Set("x", "a");
        var dataA = await vars.Get("x");
        vars.Set("x", "b");
        var dataB = await vars.Get("x");

        await Assert.That(ReferenceEquals(dataA, dataB)).IsFalse();
        await Assert.That(dataA.Peek()?.ToString()).IsEqualTo("a"); // old binding untouched
        await Assert.That(dataB.Peek()?.ToString()).IsEqualTo("b");
    }


}
