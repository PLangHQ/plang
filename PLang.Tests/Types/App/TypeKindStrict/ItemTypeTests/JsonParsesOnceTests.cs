namespace PLang.Tests.App.TypeKindStrict.ItemTypeTests;

// Stage 4 (kind-is-type, decision 792): json is `{item, kind: json}`. Two `.Get` calls parse once —
// the Data's MaterializeCount reaches 1 and stays there.
public class JsonParsesOnceTests
{
    private static async Task<(global::app.@this app, global::app.data.@this data)> Json(string raw)
    {
        var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        return (app, new global::app.data.@this("x", raw, new global::app.type.@this("item", "json"), context: ctx));
    }

    [Test] public async Task TwoGets_ParseOnce()
    {
        var (app, data) = await Json("{\"a\":1,\"b\":2}");
        await using var _ = app;

        var a = await data.Get("a");
        var b = await data.Get("b");

        await Assert.That((await a.Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await b.Value())?.ToString()).IsEqualTo("2");
        await Assert.That(data.MaterializeCount()).IsEqualTo(1);
    }
}
