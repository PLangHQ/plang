using Dict = global::app.type.dict.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 1 — the wiring around dict: navigator collapses into it, json writer
// disambiguates by type (no property-bag arm), and the lazy parse seam narrows
// json objects to dict (not raw Dictionary<string,object?>).
public class Stage1_DictNavigationAndWriterTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/Stage1DictNav-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static global::app.@this NewApp()
        => global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-dictnav-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Materialize_JsonObjectRoot_NarrowsToDict()
    {
        // A Data carrying raw json bytes for `{...}` (kind=json), on first navigation,
        // materializes Value as a dict — not a raw Dictionary<string,object?> (B+J).
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = Data.FromRaw("{\"port\":8080}", type.Create("object", "json", context: ctx), ctx, "cfg");
        await Assert.That((await d.Value())).IsTypeOf<Dict>();
        await Assert.That(((app.type.item.@this)(await ((Dict)(await d.Value())!).Get("port")!.Value())!).Clr<object>()).IsEqualTo(8080L);
    }

    [Test]
    public async Task DictionaryNavigator_CollapsesToValueType()
    {
        // variable/navigator/Dictionary collapses: when data.Value is dict, navigation
        // is `d.Get(key)`. The three-arm shape dispatch (IDictionary / generic IDictionary<,>
        // / JsonObject) and the reflection fallback are gone for the dict case (C).
        var u = new Dict(app.User.Context);
        u.Set(app.Data("name", "a"));
        u.Set(app.Data("age", 30L));
        var data = app.Data("u", u);

        // Navigation is the value's own job now (dict.Navigate via GetChild) — no navigator.
        await Assert.That((await (await data.GetChild("name")).Value())?.ToString()).IsEqualTo("a");
        await Assert.That(((global::app.type.number.@this)(await (await data.GetChild("age")).Value())!).Clr<long>()).IsEqualTo(30L);
        // A "count" intrinsic answers only when no real "count" key exists.
        await Assert.That(((global::app.type.number.@this)(await (await data.GetChild("count")).Value())!).ToInt32()).IsEqualTo(2);
    }
}
