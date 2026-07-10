using Comparison = global::app.data.Comparison;

namespace PLang.Tests.App.CompareRedesign;

// Stage 4 — rank is an int precedence declared on the value (item.Rank); higher
// drives. The reconcile (item.Compare) picks the higher-ranked side to Order and
// the lower operand coerces into it. Specificity: number(300) > text(100),
// date(500) > text, text is the floor. Async reconcile: both operands are
// materialized (Value parsed) before ranking — rank reads the item, not the type.
public class Stage4_RankTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-stage4r-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static Data D(global::app.@this app, object? v, string typeName)
        => new("x", v, global::PLang.Tests.TestApp.SharedContext.Type.Create(typeName), context: app.User.Context);

    [Test]
    public async Task Rank_NumberOverText_DateOverText_TextIsFloor()
    {
        // precedence via the int rank on the value: number > text, date > text, text == text
        await using var app = NewApp();
        var num = D(app, 5, "number"); var txt = D(app, "5", "text");
        var date = D(app, new System.DateOnly(2024,1,1), "date");
        await Assert.That(num.Peek().Rank > txt.Peek().Rank).IsTrue();    // number drives text
        await Assert.That(date.Peek().Rank > txt.Peek().Rank).IsTrue();   // date drives text
        await Assert.That(txt.Peek().Rank).IsEqualTo(D(app, "a", "text").Peek().Rank);   // text is the floor
    }

    [Test]
    public async Task Rank_IsIntPrecedenceOnTheValue()
    {
        // rank lives on the value as an int property (item.Rank) — not a method on the type taking Data
        var p = typeof(global::app.type.item.@this).GetProperty("Rank");
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.PropertyType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Compare_MaterializesBothOperands()
    {
        // async reconcile orders the real values, so comparing forces both operands to be read
        await using var app = NewApp();
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-rank-" + System.Guid.NewGuid().ToString("N")[..8]);
        await using var app2 = new global::app.@this(root);
        var p = new global::app.type.item.path.file.@this(System.IO.Path.Combine(root, "cfg.json"), app2.User.Context);
        await (await p.WriteText("5")).IsSuccess();
        var pending = await new global::app.channel.type.file.@this(p).Read();   // raw-backed, unparsed
        var other = D(app2, 5, "number");
        _ = await pending.Compare(other);
        await Assert.That(pending.MaterializeCount()).IsGreaterThanOrEqualTo(1);   // compare reads the value
    }

    [Test]
    public async Task ItemBase_DoesNotImplementStaticCompare()
    {
        // ordering is opt-in per concrete type via the instance Compare/Order; the item base
        // declares NO static two-operand comparison and NO static rank.
        var t = typeof(global::app.type.item.@this);
        await Assert.That(t.GetMethod("Compare", new[] { typeof(object), typeof(object) })).IsNull();
        await Assert.That(t.GetProperty("CompareRank",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)).IsNull();
    }
}
