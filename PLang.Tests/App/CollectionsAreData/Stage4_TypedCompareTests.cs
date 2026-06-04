namespace PLang.Tests.App.CollectionsAreData;

// Stage 4 — typed compare on the type, one compare path.
// Operator.Compare relocates onto the type (number/datetime/duration/text own ordering),
// and data.sort routes through the same entry. Compare contract is settled:
// natural order within a type, nulls last, mixed value types throw, equality-only types
// (dict/list/bool/table) error on Compare but work for Equals/group/unique.
public class Stage4_TypedCompareTests
{
    [Test]
    public async Task Compare_TwoNumbers_NaturalNumericOrder()
    {
        // number's own Compare gives natural numeric order (1 < 2 < 10), not lexical.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_NumbersAcrossKinds_WidensCorrectly()
    {
        // long vs decimal, int vs double — numeric widening yields the right total order
        // (5 == 5m, 5 < 5.1m). Existing IsNumeric coercion preserved.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_TwoDatetimes_Chronological()
    {
        // datetime owns chronological compare (earlier < later), independent of representation.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_TwoDurations_ByLength()
    {
        // duration compares by length (1s < 1m < 1h).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_TwoTexts_Lexical()
    {
        // text compares lexically ("a" < "b"); culture-invariant (no surprises across locales).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_NullElement_SortsLast()
    {
        // Compare(non-null, null) < 0 and Compare(null, non-null) > 0 in both directions — nulls last.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_TwoDifferentValueTypes_Throws()
    {
        // Compare(number, datetime) throws "cannot order X against Y" — no invented cross-type order.
        // The if-path coercions (numeric widening, string↔number) are preserved separately.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_EqualityOnlyType_Throws()
    {
        // dict/list/bool/table are equality-only; Compare on them throws a clear error. sort
        // on a list of these types throws upstream; group/unique still work via Equals.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Equals_EqualityOnlyType_WorksForGroupAndUnique()
    {
        // Equals on dict/list/bool/table works structurally for group/unique buckets.
        // The type implements only what's meaningful — equality without ordering.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Compare_OnePath_IfOperatorsAndDataSortAgree()
    {
        // The relocated compare is the single source of truth: Operator.cs `>` / `<` / `==`
        // and data.sort both call through it. `if a.age > b.age` and `sort by "age"` agree
        // for every supported type (G).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
