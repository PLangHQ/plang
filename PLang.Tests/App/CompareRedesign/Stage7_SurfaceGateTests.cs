namespace PLang.Tests.App.CompareRedesign;

// Stage 7 — full public-surface typing + the PLNG-style build gate. A public
// member of an `item.@this` subtype returning raw CLR is an error. Internal/
// private untouched; predicates return `@bool`; engine plumbing (`IsLeaf`,
// normalize dispatch) is `internal` rather than exempted. The only standing
// exemption is the gated per-type interop accessor (`path.Absolute` after
// `Authorize`). The gate's warning list is the conversion worklist.
public class Stage7_SurfaceGateTests
{
    [Test]
    public async Task Gate_PublicItemSubtypeMember_ReturningString_FailsBuild()
    {
        // PLNG-style probe: a test fixture file declaring `public string Foo => ...` on an item.@this subtype fails the build
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Gate_PublicItemSubtypeMember_ReturningInt_FailsBuild()
    {
        // covers int/long/bool/byte[]/Dictionary/List — every raw CLR return on a public item member
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Gate_IsTruthyReturnsAtBool_PassesGate()
    {
        // predicates return @bool (no carve-out); IsTruthy : @bool is the canonical example
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Gate_InternalPlumbing_IsLeaf_Untouched()
    {
        // gate's scope is public-only — `internal bool IsLeaf` and normalize dispatch are not flagged
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Gate_GatedInteropAccessor_PathAbsolute_Exempt()
    {
        // path.Absolute after `await Authorize(verb)` is the standing exemption — gate does not flag it
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathAbsolute_PublicSurface_IsPath_NotString()
    {
        // !absolute returns a `path` projection (gated, unserialised); the raw string lives at internal .Absolute
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task TextLength_ReturnsNumber_NotInt()
    {
        // %text!length% returns a `number`, not boxed int
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task DictKeys_ReturnsListOfText_NotIEnumerableString()
    {
        // %dict!keys% returns list<text>
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ListCount_ReturnsNumber_NotInt()
    {
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task FileSize_ReturnsNumber_NotLong()
    {
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
