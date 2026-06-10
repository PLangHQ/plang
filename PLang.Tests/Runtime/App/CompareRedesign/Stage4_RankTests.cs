using Comparison = global::app.data.Comparison;

namespace PLang.Tests.App.CompareRedesign;

// Stage 4 — static rank lives on the type. Data never compares ranks itself;
// it asks `this.Type.Rank(other)` (the whole other operand, never `other.Type`)
// and receives the driving type. Specificity ordering: number > text,
// date-family > text, text is the floor. Ranking never forces a value read.
public class Stage4_RankTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-stage4r-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static Data D(global::app.@this app, object? v, string typeName)
        => new("x", v, global::app.type.@this.FromName(typeName)) { Context = app.User.Context };

    [Test]
    public async Task Rank_NumberOverText_DateOverText_TextIsFloor()
    {
        // static specificity: number.Rank vs text → number; date.Rank vs text → date; text.Rank vs text → text
        await using var app = NewApp();
        var num = D(app, 5, "number"); var txt = D(app, "5", "text");
        var date = D(app, new System.DateOnly(2024,1,1), "date");
        await Assert.That(num.Type.Rank(txt).Name).IsEqualTo("number");
        await Assert.That(txt.Type.Rank(num).Name).IsEqualTo("number");   // same driver both ways
        await Assert.That(date.Type.Rank(txt).Name).IsEqualTo("date");
        await Assert.That(txt.Type.Rank(D(app, "a", "text")).Name).IsEqualTo("text");
    }

    [Test]
    public async Task Rank_TakesWholeOtherData_NotOtherType()
    {
        // signature: `Type Rank(Data other)`, not `Rank(Type other)` — the whole operand crosses the boundary
        var m = typeof(global::app.type.@this).GetMethod("Rank");
        await Assert.That(m).IsNotNull();
        var p = m!.GetParameters();
        await Assert.That(p.Length).IsEqualTo(1);
        await Assert.That(p[0].ParameterType).IsEqualTo(typeof(Data));   // the whole operand crosses
    }

    [Test]
    public async Task Rank_NeverForcesValueRead()
    {
        // rank decided from types alone — calling rank on a pending source leaves MaterializeCount=0
        await using var app = NewApp();
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-rank-" + System.Guid.NewGuid().ToString("N")[..8]);
        await using var app2 = new global::app.@this(root);
        var p = new global::app.type.path.file.@this(System.IO.Path.Combine(root, "cfg.json"), app2.User.Context);
        await (await p.WriteText("{\"port\":8080}")).IsSuccess();
        var pending = await new global::app.channel.type.file.@this(p).Read();   // raw-backed, unparsed
        var other = D(app2, 5, "number");
        _ = pending.Type.Rank(other);
        await Assert.That(pending.MaterializeCount).IsEqualTo(0);   // rank reads types, never values
    }

    [Test]
    public async Task ItemBase_DoesNotImplementIComparableValue()
    {
        // item/this.cs:23-25 — ordering opt-in per concrete type; dict : item does not inherit an order
        // ordering is opt-in per concrete type: the item base declares NO comparison hooks,
        // so dict : item never inherits an order it can't honor.
        var t = typeof(global::app.type.item.@this);
        await Assert.That(t.GetMethod("Compare", new[] { typeof(object), typeof(object) })).IsNull();
        await Assert.That(t.GetProperty("CompareRank",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)).IsNull();
    }
}
