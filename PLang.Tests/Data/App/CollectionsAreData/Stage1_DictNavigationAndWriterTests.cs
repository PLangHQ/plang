using Dict = global::app.type.item.dict.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 1 — the wiring around dict: navigator collapses into it, json writer
// disambiguates by type (no property-bag arm), and the lazy parse seam narrows
// json objects to dict (not raw Dictionary<string,object?>).
public class Stage1_DictNavigationAndWriterTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/tmp/Stage1DictNav-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-dictnav-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

    [Test]
    public async Task ReadingIntoAJsonObject_ParsesIt_AndFindsTheMember()
    {
        // A json `{...}` stays text, written as it came; reading into it (`.port`) is the need: its kind parses the
        // json and finds the member.
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var d = new global::app.data.@this("cfg", "{\"port\":8080}", new type("text", "json"), context: ctx);
        await Assert.That((await d.Value())).IsTypeOf<app.type.item.text.@this>();
        var port = await d.Get("port");
        await Assert.That(((app.type.item.@this)(await port.Value())!).Clr<object>()).IsEqualTo(8080L);
    }

    [Test]
    public async Task DictionaryNavigator_CollapsesToValueType()
    {
        // variable/navigator/Dictionary collapses: when data.Value is dict, navigation
        // is `d.Get(key)`. The three-arm shape dispatch (IDictionary / generic IDictionary<,>
        // / JsonObject) and the reflection fallback are gone for the dict case (C).
        var u = new Dict();
        u.Set(app.Data("name", "a"));
        u.Set(app.Data("age", 30L));
        var data = app.Data("u", u);

        // Navigation is the value's own job now (dict.Navigate via GetChild) — no navigator.
        await Assert.That((await (await data.Get("name")).Value())?.ToString()).IsEqualTo("a");
        await Assert.That(((global::app.type.item.number.@this)(await (await data.Get("age")).Value())!).Clr<long>()).IsEqualTo(30L);
        // A "count" intrinsic answers only when no real "count" key exists.
        await Assert.That(((global::app.type.item.number.@this)(await (await data.Get("count")).Value())!).ToInt32()).IsEqualTo(2);
    }
}
