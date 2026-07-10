using Comparison = global::app.data.Comparison;

namespace PLang.Tests.App.CompareRedesign;

// Stage 4 — per-type `Compare` returning the `Comparison` enum. The driving
// type (from rank) coerces the other side into its kind, then orders/equates
// caller-order. `a` is left (this), `b` is right (other); `Less` means
// `this < other`, no sign flip. Same driver regardless of operand order ⇒
// antisymmetry holds. Ordering math is sync — Stage 5 awaits the values.
public class Stage4_PerTypeCompareTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-stage4-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static async Task<Comparison> Cmp(global::app.@this app, object? a, object? b,
        string? aType = null, string? bType = null)
    {
        var da = new Data("a", a, aType == null ? null : global::PLang.Tests.TestApp.SharedContext.Type.Create(aType), context: app.User.Context);
        var db = new Data("b", b, bType == null ? null : global::PLang.Tests.TestApp.SharedContext.Type.Create(bType), context: app.User.Context);
        return await da.Compare(db);
    }

    // ---------- text + number + cross-pair (prove the trio first) ----------

    [Test]
    public async Task TextCompare_OrdinalCaseInsensitive_LessEqualGreater()
    {
        await using var app = NewApp();
        await Assert.That(await Cmp(app, "apple", "Banana")).IsEqualTo(Comparison.Less);
        await Assert.That(await Cmp(app, "HELLO", "hello")).IsEqualTo(Comparison.Equal);
        await Assert.That(await Cmp(app, "pear", "Apple")).IsEqualTo(Comparison.Greater);
    }

    [Test]
    public async Task NumberCompare_NumericAcrossTower_NineLessThanTen()
    {
        // numeric, not lexical — "9" < "10" as numbers
        await using var app = NewApp();
        await Assert.That(await Cmp(app, 9, 10)).IsEqualTo(Comparison.Less);
        await Assert.That(await Cmp(app, 10L, 10)).IsEqualTo(Comparison.Equal);
        await Assert.That(await Cmp(app, 10.5, 10)).IsEqualTo(Comparison.Greater);
    }

    [Test]
    public async Task TextVsNumber_TextTenVsNine_Greater_NumericNotLexical()
    {
        // text "10" vs number 9 → Greater (number drives via rank, coerces "10" → 10)
        await using var app = NewApp();
        // number drives via rank, coerces "10" -> 10; lexical would say "10" < "9"
        await Assert.That(await Cmp(app, "10", 9, aType: "text", bType: "number")).IsEqualTo(Comparison.Greater);
    }

    [Test]
    public async Task TextVsNumber_Antisymmetric_BothDirectionsAgree()
    {
        // compare(a,b)==Less ⇔ compare(b,a)==Greater — same driver in both directions
        await using var app = NewApp();
        var ab = await Cmp(app, "10", 9, aType: "text", bType: "number");
        var ba = await Cmp(app, 9, "10", aType: "number", bType: "text");
        await Assert.That(ab).IsEqualTo(Comparison.Greater);
        await Assert.That(ba).IsEqualTo(Comparison.Less);
    }

    [Test]
    public async Task TextEqualsNumber_FiveEqFive_Equal()
    {
        // "5" == 5 → Equal (the boundary maps Equal → true for ==)
        await using var app = NewApp();
        await Assert.That(await Cmp(app, "5", 5, aType: "text", bType: "number")).IsEqualTo(Comparison.Equal);
    }

    // ---------- replicate across the 11 ----------

    [Test]
    public async Task DateCompare_Ordered() { await using var app = NewApp();
        await Assert.That(await Cmp(app, new System.DateOnly(2024,1,1), new System.DateOnly(2024,6,1), "date", "date")).IsEqualTo(Comparison.Less);
        await Assert.That(await Cmp(app, new System.DateOnly(2024,6,1), new System.DateOnly(2024,6,1), "date", "date")).IsEqualTo(Comparison.Equal); }

    [Test]
    public async Task TimeCompare_Ordered() { await using var app = NewApp();
        await Assert.That(await Cmp(app, new System.TimeOnly(9,0), new System.TimeOnly(17,30), "time", "time")).IsEqualTo(Comparison.Less); }

    [Test]
    public async Task DatetimeCompare_Ordered() { await using var app = NewApp();
        var early = System.DateTimeOffset.Parse("2024-03-15T10:30:00+00:00");
        var late  = System.DateTimeOffset.Parse("2024-03-15T11:30:00+00:00");
        await Assert.That(await Cmp(app, early, late, "datetime", "datetime")).IsEqualTo(Comparison.Less); }

    [Test]
    public async Task DurationCompare_Ordered() { await using var app = NewApp();
        await Assert.That(await Cmp(app, System.TimeSpan.FromSeconds(30), System.TimeSpan.FromMinutes(1), "duration", "duration")).IsEqualTo(Comparison.Less); }

    [Test]
    public async Task DatetimeIsoTextCoerces_BothDirections()
    {
        // datetime ↔ ISO-text — driver datetime coerces text via parse
        await using var app = NewApp();
        var dt = System.DateTimeOffset.Parse("2024-03-15T10:30:00+00:00");
        // datetime drives both directions; the ISO text coerces via its parse
        await Assert.That(await Cmp(app, "2024-03-15T09:30:00+00:00", dt, "text", "datetime")).IsEqualTo(Comparison.Less);
        await Assert.That(await Cmp(app, dt, "2024-03-15T09:30:00+00:00", "datetime", "text")).IsEqualTo(Comparison.Greater);
    }

    [Test]
    public async Task ListCompare_Lexicographic()
    {
        // list ordering by element, lexicographic
        await using var app = NewApp();
        var ctx = app.User.Context;
        static global::app.type.list.@this L(global::app.actor.context.@this c, params object[] items)
        {
            var l = new global::app.type.list.@this(c);
            foreach (var i in items) l.Add(new Data("", i, context: c));
            return l;
        }
        await Assert.That(global::app.type.list.@this.Compare(L(ctx,1,2), L(ctx,1,3))).IsEqualTo(Comparison.Less);
        await Assert.That(global::app.type.list.@this.Compare(L(ctx,1,2), L(ctx,1,2,3))).IsEqualTo(Comparison.Less); // prefix first
        await Assert.That(global::app.type.list.@this.Compare(L(ctx,2), L(ctx,1,9))).IsEqualTo(Comparison.Greater);
    }

    [Test]
    public async Task BoolEquality_NoOrder_OrderingReturnsNotEqual()
    {
        // bool answers Equal/NotEqual; ordering returns NotEqual (boundary errors on <,>)
        await using var app = NewApp();
        await Assert.That(await Cmp(app, true, true, "bool", "bool")).IsEqualTo(Comparison.Equal);
        // unequal bools answer NotEqual — an ordering op maps that to an error at the boundary
        await Assert.That(await Cmp(app, true, false, "bool", "bool")).IsEqualTo(Comparison.NotEqual);
    }

    [Test]
    public async Task BinaryEquality_SameByteSequence_Equal() { await using var app = NewApp();
        await Assert.That(await Cmp(app, new byte[]{1,2,3}, new byte[]{1,2,3}, "binary", "binary")).IsEqualTo(Comparison.Equal);
        await Assert.That(await Cmp(app, new byte[]{1,2,3}, new byte[]{1,2,4}, "binary", "binary")).IsEqualTo(Comparison.NotEqual); }

    [Test]
    public async Task ChoiceEquality_SameChoice_Equal() { var a = new global::app.type.item.choice.@this<global::app.goal.steps.step.ErrorOrder>(global::app.goal.steps.step.ErrorOrder.RetryFirst);
        var b = new global::app.type.item.choice.@this<global::app.goal.steps.step.ErrorOrder>(global::app.goal.steps.step.ErrorOrder.RetryFirst);
        var c = new global::app.type.item.choice.@this<global::app.goal.steps.step.ErrorOrder>(global::app.goal.steps.step.ErrorOrder.GoalFirst);
        await Assert.That(global::app.type.item.choice.@this<global::app.goal.steps.step.ErrorOrder>.Compare(a, b)).IsEqualTo(Comparison.Equal);
        await Assert.That(global::app.type.item.choice.@this<global::app.goal.steps.step.ErrorOrder>.Compare(a, c)).IsEqualTo(Comparison.NotEqual);
        await Assert.That(global::app.type.item.choice.@this<global::app.goal.steps.step.ErrorOrder>.Compare(a, "RetryFirst")).IsEqualTo(Comparison.Equal); // by name
    }

    [Test]
    public async Task DictEquality_SameShape_Equal_DifferentShape_NotEqual()
    {
        // dict is equality-only; same shape → Equal, different → NotEqual; ordering → NotEqual (errors at boundary)
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d1 = global::app.type.item.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var d2 = global::app.type.item.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var d3 = global::app.type.item.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 2 }, ctx);
        await Assert.That(await Cmp(app, d1, d2, "dict", "dict")).IsEqualTo(Comparison.Equal);
        await Assert.That(await Cmp(app, d1, d3, "dict", "dict")).IsEqualTo(Comparison.NotEqual);
    }

    [Test]
    public async Task NullVsAnyType_EqualOrNotEqual_NeverIncomparable()
    {
        // null carve-out — anything vs null is equality-comparable for every type
        await using var app = NewApp();
        await Assert.That(await Cmp(app, null, null)).IsEqualTo(Comparison.Equal);
        await Assert.That(await Cmp(app, 5, null, "number", null)).IsEqualTo(Comparison.NotEqual);
        var ctx = app.User.Context;
        var d = global::app.type.item.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        await Assert.That(await Cmp(app, d, null, "dict", null)).IsEqualTo(Comparison.NotEqual);  // even dict vs null
    }

    [Test]
    public async Task NullsLast_InSortOrdering()
    {
        // sort places null entries last
        await using var app = NewApp();
        var ctx = app.User.Context;
        var list = new global::app.type.list.@this(ctx);
        list.Add(new Data("", 3, context: ctx));
        list.Add(new Data("", null, context: ctx));
        list.Add(new Data("", 1, context: ctx));
        await list.SortByValue(descending: false);
        await Assert.That((await list.At(0)!.Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await list.At(1)!.Value())?.ToString()).IsEqualTo("3");
        await Assert.That(await (await list.At(2)!.Value())!.IsEmpty()).IsTrue();   // nulls last
    }

    [Test]
    public async Task NonCoercibleCrossType_DictVsNumber_Incomparable()
    {
        // driver can't coerce → Incomparable; symmetric (same in both directions)
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::app.type.item.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        await Assert.That(await Cmp(app, d, 5, "dict", "number")).IsEqualTo(Comparison.Incomparable);
        await Assert.That(await Cmp(app, 5, d, "number", "dict")).IsEqualTo(Comparison.Incomparable); // symmetric
    }

    [Test]
    public async Task Compare_Sync_OnMaterialisedValues_NoIo()
    {
        // per-type Compare runs no I/O — sync over already-materialised values
        // the per-type hook is sync by signature: static Comparison Compare(object?, object?)
        var hook = typeof(global::app.type.item.number.@this).GetMethod("Compare",
            new[] { typeof(object), typeof(object) });
        await Assert.That(hook).IsNotNull();
        await Assert.That(hook!.ReturnType).IsEqualTo(typeof(Comparison));   // no Task/ValueTask
    }
}
