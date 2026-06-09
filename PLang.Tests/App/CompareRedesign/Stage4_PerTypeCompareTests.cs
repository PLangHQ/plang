namespace PLang.Tests.App.CompareRedesign;

// Stage 4 — per-type `Compare` returning the `Comparison` enum. The driving
// type (from rank) coerces the other side into its kind, then orders/equates
// caller-order. `a` is left (this), `b` is right (other); `Less` means
// `this < other`, no sign flip. Same driver regardless of operand order ⇒
// antisymmetry holds. Ordering math is sync — Stage 5 awaits the values.
public class Stage4_PerTypeCompareTests
{
    // ---------- text + number + cross-pair (prove the trio first) ----------

    [Test]
    public async Task TextCompare_OrdinalCaseInsensitive_LessEqualGreater()
    {
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task NumberCompare_NumericAcrossTower_NineLessThanTen()
    {
        // numeric, not lexical — "9" < "10" as numbers
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task TextVsNumber_TextTenVsNine_Greater_NumericNotLexical()
    {
        // text "10" vs number 9 → Greater (number drives via rank, coerces "10" → 10)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task TextVsNumber_Antisymmetric_BothDirectionsAgree()
    {
        // compare(a,b)==Less ⇔ compare(b,a)==Greater — same driver in both directions
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task TextEqualsNumber_FiveEqFive_Equal()
    {
        // "5" == 5 → Equal (the boundary maps Equal → true for ==)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    // ---------- replicate across the 11 ----------

    [Test]
    public async Task DateCompare_Ordered() { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test]
    public async Task TimeCompare_Ordered() { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test]
    public async Task DatetimeCompare_Ordered() { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test]
    public async Task DurationCompare_Ordered() { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test]
    public async Task DatetimeIsoTextCoerces_BothDirections()
    {
        // datetime ↔ ISO-text — driver datetime coerces text via parse
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ListCompare_Lexicographic()
    {
        // list ordering by element, lexicographic
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BoolEquality_NoOrder_OrderingReturnsNotEqual()
    {
        // bool answers Equal/NotEqual; ordering returns NotEqual (boundary errors on <,>)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BinaryEquality_SameByteSequence_Equal() { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test]
    public async Task ChoiceEquality_SameChoice_Equal() { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test]
    public async Task DictEquality_SameShape_Equal_DifferentShape_NotEqual()
    {
        // dict is equality-only; same shape → Equal, different → NotEqual; ordering → NotEqual (errors at boundary)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task NullVsAnyType_EqualOrNotEqual_NeverIncomparable()
    {
        // null carve-out — anything vs null is equality-comparable for every type
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task NullsLast_InSortOrdering()
    {
        // sort places null entries last
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task NonCoercibleCrossType_DictVsNumber_Incomparable()
    {
        // driver can't coerce → Incomparable; symmetric (same in both directions)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_Sync_OnMaterialisedValues_NoIo()
    {
        // per-type Compare runs no I/O — sync over already-materialised values
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
