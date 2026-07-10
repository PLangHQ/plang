using Comparison = global::app.data.Comparison;

namespace PLang.Tests.App.CompareRedesign;

// Stage 5 — `data.Compare(other)` is the one public entry. Awaits both values
// through the door (the only async hops), picks the driving type via rank,
// runs the winner's sync `Compare(a, b)` in caller order — no winner-vs-loser
// flip. Dispatch routes through the existing name→family path; no
// `Type.Name` switch.
public class Stage5_DataCompareEntryTests
{
    private static global::app.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-stage5-" + System.Guid.NewGuid().ToString("N")[..8]);
        return new(root);
    }

    private static Data D(global::app.@this app, object? v, string typeName)
        => new("x", v, global::PLang.Tests.TestApp.SharedContext.Type.Create(typeName), context: app.User.Context);

    [Test]
    public async Task DataCompare_CallerOrder_LessMeansThisLessThanOther()
    {
        // a.Compare(b) → Less ⇔ a < b, regardless of which type was the driver
        await using var app = NewApp(out _);
        // text drives neither pair the same way — but Less always means this < other
        await Assert.That(await D(app, "9", "text").Compare(D(app, 10, "number"))).IsEqualTo(Comparison.Less);
        await Assert.That(await D(app, 10, "number").Compare(D(app, "9", "text"))).IsEqualTo(Comparison.Greater);
    }

    [Test]
    public async Task DataCompare_AwaitsBothValues_ThenSyncCompare()
    {
        // exactly two awaits (this.Value(), other.Value()); per-type Compare is sync
        await using var app = NewApp(out var root);
        var p = new global::app.type.item.path.file.@this(System.IO.Path.Combine(root, "n.json"), app.User.Context);
        await (await p.WriteText("42")).IsSuccess();
        var pending = await new global::app.channel.type.file.@this(p).Read();   // raw-backed
        await Assert.That(pending.MaterializeCount()).IsEqualTo(0);
        var result = await pending.Compare(D(app, 42, "number"));
        await Assert.That(pending.MaterializeCount()).IsEqualTo(1);   // exactly one await-read per operand
        await Assert.That(result).IsEqualTo(Comparison.Equal);
    }

    [Test]
    public async Task DataCompare_MaterializesBeforeRanking()
    {
        // async reconcile: Compare awaits Value() on both operands, then ranks the real items —
        // the pending source is read (rank is an int on the value, not on the type)
        await using var app = NewApp(out var root);
        var p = new global::app.type.item.path.file.@this(System.IO.Path.Combine(root, "n.json"), app.User.Context);
        await (await p.WriteText("42")).IsSuccess();
        var pending = await new global::app.channel.type.file.@this(p).Read();
        _ = await pending.Compare(D(app, 5, "number"));       // the compare reads both values
        await Assert.That(pending.MaterializeCount()).IsGreaterThanOrEqualTo(1);   // pending is read
    }

    [Test]
    public async Task DataCompare_NoTypeNameSwitch_RoutesViaNameFamily()
    {
        // analyzer/grep gate: dispatch has no `Type.Name == "..."` switch; reuses App.Type[Name] routing
        // grep gate: the dispatch (type entity Rank/Compare + the registry) carries no
        // `Name == "..."` type-name switch — routing reuses the name→family registry.
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang", "app")))
            dir = Directory.GetParent(dir)?.FullName;
        // compare dispatch lives on the value now (item.Compare/Order, rank-driven) — no type-name switch
        var itemSrc = await File.ReadAllTextAsync(Path.Combine(dir!, "PLang", "app", "type", "item", "this.cs"));
        var orderIdx = itemSrc.IndexOf("ValueTask<global::app.data.Comparison> Compare", System.StringComparison.Ordinal);
        await Assert.That(itemSrc.Substring(orderIdx, 900)).DoesNotContain("Name == \"");
        var dataSrc = await File.ReadAllTextAsync(Path.Combine(dir!, "PLang", "app", "data", "this.cs"));
        var compareIdx = dataSrc.IndexOf("public async ValueTask<Comparison> Compare", System.StringComparison.Ordinal);
        var body = dataSrc.Substring(compareIdx, 900);
        await Assert.That(body).DoesNotContain("Name == \"");
    }
}
