
namespace PLang.Tests.App.CollectionsAreData;

// Stage 2 — `set` rebinds, not mutates. The two raw branches of Variables.Set
// (frame-overlay :199, underlying-dict :227) must mint a new Data on a same-type
// set and carry OnCreate/OnChange/OnDelete subscribers across — matching the
// Data-value branch (:137-191) that already rebinds. Pin both in isolation so the
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
        var dataA = vars.Get("x");
        vars.Set("x", "b");
        var dataB = vars.Get("x");

        await Assert.That(ReferenceEquals(dataA, dataB)).IsFalse();
        await Assert.That((string?)dataA.ScalarValue).IsEqualTo("a"); // old binding untouched
        await Assert.That((string?)dataB.ScalarValue).IsEqualTo("b");
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
        var dataA = vars.Get("x");
        vars.Set("x", "b");
        var dataB = vars.Get("x");

        await Assert.That(ReferenceEquals(dataA, dataB)).IsFalse();
        await Assert.That((string?)dataA.ScalarValue).IsEqualTo("a"); // old binding untouched
        await Assert.That((string?)dataB.ScalarValue).IsEqualTo("b");
    }

    [Test]
    public async Task Set_Rebind_CarriesSubscribersAcrossByName()
    {
        // OnCreate / OnChange / OnDelete subscribers registered against the variable name
        // remain attached after a Set that mints a new Data. The Data-value branch already
        // does this; the raw branches must match.
        await using var app = NewApp();
        var vars = app.User.Context.Variable;

        vars.Set("x", "a");
        var dataA = vars.Get("x");
        dataA.OnChange.Add((_, _) => { });
        dataA.OnDelete.Add(_ => { });

        vars.Set("x", "b");
        var dataB = vars.Get("x");

        // The subscriber lists follow the name onto the new binding (carried by reference).
        await Assert.That(ReferenceEquals(dataA.OnChange, dataB.OnChange)).IsTrue();
        await Assert.That(ReferenceEquals(dataA.OnDelete, dataB.OnDelete)).IsTrue();
        await Assert.That(dataB.OnChange.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Set_Rebind_FiresOnChange_NotInPlaceMutation()
    {
        // OnChange fires on the rebind (mint-and-replace) — not on a mutation that never
        // happened. Distinguishes the new rebind path from the old `existing.Value = value`
        // mutation that did fire OnChange but for the wrong reason.
        await using var app = NewApp();
        var vars = app.User.Context.Variable;

        vars.Set("x", "a");
        var dataA = vars.Get("x");
        Data? firedWith = null;
        dataA.OnChange.Add((_, replacement) => firedWith = replacement);

        vars.Set("x", "b");
        var dataB = vars.Get("x");

        // OnChange fired with the new (rebound) Data, which is the current binding.
        await Assert.That(firedWith).IsNotNull();
        await Assert.That(ReferenceEquals(firedWith, dataB)).IsTrue();
        await Assert.That((string?)firedWith!.ScalarValue).IsEqualTo("b");
    }
}
