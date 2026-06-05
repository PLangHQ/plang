using Cmp = global::app.data.Compare;
using DictV = global::app.type.dict.@this;
using ListV = global::app.type.list.@this;
using Op = global::app.module.condition.Operator;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 4 — typed compare on the type, one compare path.
// app.data.Compare.Order is the single entry both the condition operators
// (Operator) and list.sort route through. Settled contract: natural order within
// a type, nulls last, mixed value types throw, equality-only types (dict/list/
// bool/table) error on Order but work for AreEqual/group/unique.
public class Stage4_TypedCompareTests
{
    private static Data D(object? v) => new("", v);

    [Test]
    public async Task Compare_TwoNumbers_NaturalNumericOrder()
    {
        // number's own Compare gives natural numeric order (1 < 2 < 10), not lexical.
        await Assert.That(Cmp.Order(D(1L), D(2L))).IsLessThan(0);
        await Assert.That(Cmp.Order(D(10L), D(2L))).IsGreaterThan(0); // lexical would put "10" < "2"
        await Assert.That(Cmp.Order(D(2L), D(2L))).IsEqualTo(0);
    }

    [Test]
    public async Task Compare_NumbersAcrossKinds_WidensCorrectly()
    {
        // long vs decimal, int vs double — numeric widening yields the right total order.
        await Assert.That(Cmp.Order(D(5L), D(5m))).IsEqualTo(0);
        await Assert.That(Cmp.Order(D(5L), D(5.1m))).IsLessThan(0);
        await Assert.That(Cmp.Order(D(5), D(5.0d))).IsEqualTo(0);
    }

    [Test]
    public async Task Compare_TwoDatetimes_Chronological()
    {
        // datetime owns chronological compare (earlier < later).
        var early = new System.DateTimeOffset(2020, 1, 1, 0, 0, 0, System.TimeSpan.Zero);
        var late = new System.DateTimeOffset(2024, 6, 1, 0, 0, 0, System.TimeSpan.Zero);
        await Assert.That(Cmp.Order(D(early), D(late))).IsLessThan(0);
        await Assert.That(Cmp.Order(D(late), D(early))).IsGreaterThan(0);
    }

    [Test]
    public async Task Compare_TwoDurations_ByLength()
    {
        // duration compares by length (1s < 1m < 1h).
        await Assert.That(Cmp.Order(D(System.TimeSpan.FromSeconds(1)), D(System.TimeSpan.FromMinutes(1)))).IsLessThan(0);
        await Assert.That(Cmp.Order(D(System.TimeSpan.FromHours(1)), D(System.TimeSpan.FromMinutes(1)))).IsGreaterThan(0);
    }

    [Test]
    public async Task Compare_TwoTexts_Lexical()
    {
        // text compares lexically and culture-invariant ("a" < "b").
        await Assert.That(Cmp.Order(D("a"), D("b"))).IsLessThan(0);
        await Assert.That(Cmp.Order(D("b"), D("a"))).IsGreaterThan(0);
        await Assert.That(Cmp.Order(D("a"), D("a"))).IsEqualTo(0);
    }

    [Test]
    public async Task Compare_TextCaseInsensitive_OrderAndEqualsAgree()
    {
        // One case policy across ordering and equality: "a" and "A" are equal AND
        // order as equal — no trichotomy violation (equal-and-greater can't coexist),
        // so a sort+unique pipeline stays consistent.
        await Assert.That(Cmp.AreEqual(D("a"), D("A"))).IsTrue();
        await Assert.That(Cmp.Order(D("a"), D("A"))).IsEqualTo(0);
        // Lexical ordering still holds across distinct letters, case ignored.
        await Assert.That(Cmp.Order(D("apple"), D("Banana"))).IsLessThan(0);
        await Assert.That(Cmp.Order(D("Banana"), D("apple"))).IsGreaterThan(0);
    }

    [Test]
    public async Task Contains_StructuralDictEquality_MatchesEqualsPath()
    {
        // `contains` / `in` route through the same structural equality as `==`,
        // so a list containing a structurally-equal dict matches (not reference-equal).
        var inner = new DictV(); inner.Set(new Data("city", "Reyk"));
        var list = new ListV(); list.Add(new Data("", inner));
        var probe = new DictV(); probe.Set(new Data("city", "Reyk"));

        await Assert.That(await new Op("contains").Evaluate(D(list), D(probe))).IsTrue();
        await Assert.That(await new Op("in").Evaluate(D(probe), D(list))).IsTrue();

        var miss = new DictV(); miss.Set(new Data("city", "Oslo"));
        await Assert.That(await new Op("contains").Evaluate(D(list), D(miss))).IsFalse();
    }

    [Test]
    public async Task Compare_NullElement_SortsLast()
    {
        // Compare(non-null, null) < 0 and Compare(null, non-null) > 0 — nulls last.
        await Assert.That(Cmp.Order(D(5L), D(null))).IsLessThan(0);
        await Assert.That(Cmp.Order(D(null), D(5L))).IsGreaterThan(0);
        await Assert.That(Cmp.Order(D(null), D(null))).IsEqualTo(0);
    }

    [Test]
    public async Task Compare_TwoDifferentValueTypes_Throws()
    {
        // Compare(number, datetime) throws — no invented cross-type order.
        await Assert.That(() => Cmp.Order(D(5L), D(System.DateTimeOffset.UnixEpoch)))
            .Throws<Cmp.NotOrderableException>();
    }

    [Test]
    public async Task Compare_EqualityOnlyType_Throws()
    {
        // dict/bool are equality-only; Order on them throws a clear error.
        // (list is orderable — see Compare_TwoLists_Lexicographic.)
        await Assert.That(() => Cmp.Order(D(new DictV()), D(new DictV()))).Throws<Cmp.NotOrderableException>();
        await Assert.That(() => Cmp.Order(D(true), D(false))).Throws<Cmp.NotOrderableException>();
    }

    [Test]
    public async Task Compare_TwoLists_Lexicographic()
    {
        // list owns IOrderableValue — item-by-item, first differing pair decides;
        // a prefix sorts before the longer list.
        static ListV L(params long[] xs)
        {
            var l = new ListV();
            foreach (var x in xs) l.Add(new Data("", x));
            return l;
        }
        await Assert.That(Cmp.Order(D(L(1, 2, 3)), D(L(1, 3)))).IsLessThan(0);     // 2 < 3 at item 2
        await Assert.That(Cmp.Order(D(L(1, 2)), D(L(1, 2, 3)))).IsLessThan(0);     // prefix sorts first
        await Assert.That(Cmp.Order(D(L(1, 2, 3)), D(L(9)))).IsLessThan(0);        // 1 < 9 at item 1 (not by length)
        await Assert.That(Cmp.Order(D(L(1, 2)), D(L(1, 2)))).IsEqualTo(0);
        // A list can't be ordered against a scalar.
        await Assert.That(() => Cmp.Order(D(L(1)), D(5L))).Throws<Cmp.NotOrderableException>();
    }

    [Test]
    public async Task Equals_EqualityOnlyType_WorksForGroupAndUnique()
    {
        // AreEqual on dict/list works structurally — equivalent collections collapse.
        var a = new DictV(); a.Set(new Data("city", "Reyk"));
        var b = new DictV(); b.Set(new Data("city", "Reyk"));
        var c = new DictV(); c.Set(new Data("city", "Oslo"));
        await Assert.That(Cmp.AreEqual(D(a), D(b))).IsTrue();
        await Assert.That(Cmp.AreEqual(D(a), D(c))).IsFalse();

        var la = new ListV(); la.Add(new Data("", 1L)); la.Add(new Data("", 2L));
        var lb = new ListV(); lb.Add(new Data("", 1L)); lb.Add(new Data("", 2L));
        await Assert.That(Cmp.AreEqual(D(la), D(lb))).IsTrue();
        await Assert.That(Cmp.AreEqual(D(true), D(true))).IsTrue();
    }

    [Test]
    public async Task Compare_OnePath_IfOperatorsAndDataSortAgree()
    {
        // Operator.cs `>` / `<` and Compare.Order are the same source of truth.
        var gt = new Op(">");
        var lt = new Op("<");
        await Assert.That(await gt.Evaluate(D(10L), D(2L))).IsTrue();   // 10 > 2 numerically
        await Assert.That(await lt.Evaluate(D(10L), D(2L))).IsFalse();
        // Agreement with Order's sign.
        await Assert.That(Cmp.Order(D(10L), D(2L)) > 0).IsEqualTo(await gt.Evaluate(D(10L), D(2L)));
    }
}
