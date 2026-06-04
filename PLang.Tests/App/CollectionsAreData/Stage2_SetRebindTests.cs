namespace PLang.Tests.App.CollectionsAreData;

// Stage 2 — `set` rebinds, not mutates. The two raw branches of Variables.Set
// (frame-overlay :199, underlying-dict :227) must mint a new Data on a same-type
// set and carry OnCreate/OnChange/OnDelete subscribers across — matching the
// Data-value branch (:137-191) that already rebinds. Pin both in isolation so the
// alias bug doesn't reappear inside channel-fire or parallel-foreach flows.
public class Stage2_SetRebindTests
{
    [Test]
    public async Task Set_RawFrameOverlayBranch_RebindsNotMutates()
    {
        // The :199 branch path (frame-overlay raw branch). A same-type Set with a raw value
        // must mint a new Data and replace the binding — the previously-bound Data instance
        // (e.g. one held by a list) must compare reference-unequal to the new binding (M).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Set_RawUnderlyingDictBranch_RebindsNotMutates()
    {
        // The :227 branch path (underlying-dict raw branch). Same shape as the frame-overlay
        // test, but exercising the alternate raw-branch arm so a future split doesn't leave
        // one mutating in place.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Set_Rebind_CarriesSubscribersAcrossByName()
    {
        // OnCreate / OnChange / OnDelete subscribers registered against the variable name
        // remain attached after a Set that mints a new Data. The Data-value branch already
        // does this; the raw branches must match.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Set_Rebind_FiresOnChange_NotInPlaceMutation()
    {
        // OnChange fires on the rebind (mint-and-replace) — not on a mutation that never
        // happened. Distinguishes the new rebind path from the old `existing.Value = value`
        // mutation that did fire OnChange but for the wrong reason.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
