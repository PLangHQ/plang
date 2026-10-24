using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Stage 4 (kind-is-type, decision 792): json is `{item, kind: json}` holding its raw bytes until first use;
// navigation parses once into a plang dict (or list); after the read the value narrows (decision 787).
public class JsonLazyNavigationTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-jsonlazy-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor);

    [Test] public async Task Navigation_ParsesJsonIntoAPlangDict_AndNarrowsAfterTheRead()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var d = new data("cfg", "{\"port\":8080}", new type("item", "json"), context: ctx);

        // While unread: an item of kind json.
        await Assert.That(d.Type.Name).IsEqualTo("item");
        await Assert.That(d.Type.kind.Name).IsEqualTo("json");

        var port = await d.Get("port");
        await Assert.That((await port.Value())?.ToString()).IsEqualTo("8080");

        // After the read: narrowed to a plang dict (787 — a value's type is what it is now).
        await Assert.That(d.Type.Name).IsEqualTo("dict");
    }
}
