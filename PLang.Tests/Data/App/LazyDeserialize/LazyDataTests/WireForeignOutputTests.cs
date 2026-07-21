using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Wire = global::app.type.item.wire.@this;
using Plang = global::app.channel.serializer.plang.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

/// <summary>
/// A wire (a still-packed value from a .pr) written into a FOREIGN format (here the Text serializer,
/// as llm.query does with its Schema) must materialize and render THROUGH ITS DECODED VALUE'S OWN
/// SHAPE — a leaf via Write, a dict/list via structural Output. Before the fix, wire.Write sends the
/// decoded value through the leaf door (Write) unconditionally, so a non-leaf (dict/list/object)
/// throws "no bare wire form". These pin the shape for every kind a schema-like slot can hold.
/// </summary>
public class WireForeignOutputTests
{
    private static async Task<string> TextOut(app.@this app, string rawSlice, string typeName, string? kind = null)
    {
        var ctx = app.User.Context;
        var type = global::app.type.@this.Create(typeName, kind);
        var wire = new Wire(rawSlice, type, ctx, new Plang(ctx));   // packed slice, plang-captured
        var data = new global::app.data.@this("slot", wire, context: ctx);

        using var ms = new System.IO.MemoryStream();
        await ctx.Actor.Channel.Serializers.Text.SerializeAsync(ms, data);   // FOREIGN format
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test] public async Task WireLeafString_ForeignText_Works()   // control — leaves are fine today
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var text = await TextOut(app, "\"hello\"", "text");
        await Assert.That(text).Contains("hello");
    }

    [Test] public async Task WireDict_ForeignText_RendersJson_NotThrows()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var text = await TextOut(app, "{\"a\":1}", "dict");
        await Assert.That(text).Contains("\"a\"");
        await Assert.That(text).Contains("1");
    }

    [Test] public async Task WireList_ForeignText_RendersJson_NotThrows()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var text = await TextOut(app, "[1,2,3]", "list");
        await Assert.That(text).Contains("1");
        await Assert.That(text).Contains("3");
    }

    [Test] public async Task WireObject_ForeignText_RendersJson_NotThrows()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var text = await TextOut(app, "{\"name\":\"x\"}", "object");
        await Assert.That(text).Contains("name");
    }
}
