using PLang.Tests.App.Serialization;
using Dict = global::app.type.dict.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 1 — the wiring around dict: navigator collapses into it, json writer
// disambiguates by type (no property-bag arm), and the lazy parse seam narrows
// json objects to dict (not raw Dictionary<string,object?>).
public class Stage1_DictNavigationAndWriterTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-dictnav-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Materialize_JsonObjectRoot_NarrowsToDict()
    {
        // A Data carrying raw json bytes for `{...}` (kind=json), on first navigation,
        // materializes Value as a dict — not a raw Dictionary<string,object?> (B+J).
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = Data.FromRaw("{\"port\":8080}", type.Create("object", "json", context: ctx), ctx, "cfg");
        d.ForceMaterialize();
        await Assert.That(d.Value).IsTypeOf<Dict>();
        await Assert.That((long)((Dict)d.Value!).Get("port")!.Value!).IsEqualTo(8080L);
    }

    [Test]
    public async Task DictionaryNavigator_CollapsesToValueType()
    {
        // variable/navigator/Dictionary collapses: when data.Value is dict, navigation
        // is `d.Get(key)`. The three-arm shape dispatch (IDictionary / generic IDictionary<,>
        // / JsonObject) and the reflection fallback are gone for the dict case (C).
        var u = new Dict();
        u.Set(new Data("name", "a"));
        u.Set(new Data("age", 30L));
        var data = new Data("u", u);

        var nav = new global::app.variable.navigator.Dictionary();
        await Assert.That(nav.CanNavigate(data)).IsTrue();
        await Assert.That((string?)nav.Navigate(data, "name").Value).IsEqualTo("a");
        await Assert.That((long)nav.Navigate(data, "age").Value!).IsEqualTo(30L);
        // A "count" intrinsic answers only when no real "count" key exists.
        await Assert.That((int)nav.Navigate(data, "count").Value!).IsEqualTo(2);
    }

    [Test]
    public async Task JsonWriter_PropertyBagArm_Deleted()
    {
        // writer.cs no longer has `case List<app.data.@this> propertyBag:` (E).
        // A List<Data> now serializes as a JSON array; only a dict routes to `{}`.
        var list = new List<Data> { new("a", 1L), new("b", 2L) };
        var json = NormalizePipelineHelper.SerializeValueSlot(list);
        await Assert.That(json.StartsWith("[")).IsTrue();
        await Assert.That(json.StartsWith("{")).IsFalse();
    }

    [Test]
    public async Task NormalizeObject_DomainRecord_ReturnsDict()
    {
        // data/this.Normalize.cs's NormalizeObject returns a dict for a C# domain record
        // (e.g. identity) — not List<@this> (F). One object shape across the wire.
        var identity = new global::app.module.identity.Identity { Name = "alice", PublicKey = "pk" };
        var normalized = new Data("", identity).Normalize();
        await Assert.That(normalized).IsTypeOf<Dict>();
        var d = (Dict)normalized!;
        await Assert.That(d.Has("name")).IsTrue();
        await Assert.That((string?)d.Get("name")!.Value).IsEqualTo("alice");
    }
}
