namespace PLang.Tests.App.Types;

// A type's plang surface: each member a plang value under one plain name, the catalog's set. A collection reads its own
// marked members only, so a row's field of the same name as a CLR member of the list still reads through to the row.
public class TypeSurfaceTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/surface-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<global::app.data.@this> Read(string path) => await new global::app.type.item.variable.@this(path).Start(Ctx);

    [Test] public async Task APath_IsReadByNameStemParentAndMime()
    {
        await Ctx.Variable.Set("p", global::app.type.item.path.@this.Resolve("/data/report.json", Ctx));

        await Assert.That((await Read("p.name")).Peek().ToString()).IsEqualTo("report.json");
        await Assert.That((await Read("p.stem")).Peek().ToString()).IsEqualTo("report");
        await Assert.That((await Read("p.mime")).Peek().ToString()).IsEqualTo("application/json");
        await Assert.That((await Read("p.parent")).Peek() is global::app.type.item.path.@this).IsTrue();
        await Assert.That((await Read("p.isFile")).IsInitialized).IsFalse();
    }

    [Test] public async Task ADatetime_ItsPartsAreNumbers_ItsWeekdayANamedText()
    {
        await Ctx.Variable.Set("dt", new global::app.type.item.datetime.@this(new System.DateTimeOffset(2026, 10, 3, 9, 30, 0, System.TimeSpan.Zero)));

        await Assert.That((await Read("dt.year")).Peek() is global::app.type.item.number.@this).IsTrue();
        await Assert.That((await Read("dt.weekday")).Peek().ToString()).IsEqualTo("Saturday");
        await Assert.That((await Read("dt.time")).Peek() is global::app.type.item.time.@this).IsTrue();
    }

    [Test] public async Task ADuration_IsTheWholeSpanInEachUnit()
    {
        await Ctx.Variable.Set("elapsed", new global::app.type.item.duration.@this(System.TimeSpan.FromSeconds(90)));

        await Assert.That((await Read("elapsed.seconds")).Peek().ToString()).IsEqualTo("90");
        await Assert.That((await Read("elapsed.minutes")).Peek().ToString()).IsEqualTo("1.5");
    }

    [Test] public async Task AList_ReadsItsOwnMembers_AndReadsThroughToItsFirstRow()
    {
        await Ctx.Variable.Set("rows", new List<object?>
        {
            new Dictionary<string, object?> { ["rank"] = "first", ["street"] = "Main" },
            new Dictionary<string, object?> { ["rank"] = "second", ["street"] = "Side" },
        });

        await Assert.That((await Read("rows.count")).Peek().ToString()).IsEqualTo("2");
        await Assert.That((await (await Read("rows.last")).Get("street")).Peek().ToString()).IsEqualTo("Side");
        // a row's field named as a public member the list has in C# (Rank) is the row's
        await Assert.That((await Read("rows.rank")).Peek().ToString()).IsEqualTo("first");
        await Assert.That((await Read("rows.street")).Peek().ToString()).IsEqualTo("Main");
        // one name for the count
        await Assert.That((await Read("rows.length")).IsInitialized).IsFalse();
    }

    // a member answering through a task, either kind, is listed as what the task completes with
    [Test] public async Task AListsAll_IsListedAsAList()
        => await Assert.That(_app.type.list["list"].Property!["all"]!.Type.Name).IsEqualTo("list");

    [Test] public async Task ADict_ARealKeyWinsOverItsOwnCount()
    {
        await Ctx.Variable.Set("d", new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 });
        await Ctx.Variable.Set("named", new Dictionary<string, object?> { ["count"] = "x" });

        await Assert.That((await Read("d.count")).Peek().ToString()).IsEqualTo("2");
        await Assert.That((await Read("named.count")).Peek().ToString()).IsEqualTo("x");
    }
}
