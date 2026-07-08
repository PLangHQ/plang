using System.Text.Json;

namespace PLang.Tests.App.Types;

// A clr carrying a JsonElement navigates by its KIND (json), not by C# reflection —
// so %doc.steps[0].index% walks the json instead of reflecting a nonexistent property.
// This is the mechanism that unblocks `plang build` (the llm plan result is a clr(json)).
public class ClrKindNavigationTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app =
        global::PLang.Tests.TestApp.Create("/tmp/clrkind-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement.Clone();

    [Test]
    public async Task ClrJsonElement_DerivesKindJson()
    {
        var ctx = _app.User.Context;
        var clr = new global::app.type.clr.@this(Json("{\"a\":1}"), ctx);
        await Assert.That(clr.Kind.ToString()).IsEqualTo("json");
    }

    [Test]
    public async Task ClrJsonElement_NavigatesObjectArrayScalar_AsJson()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(Json("{\"steps\":[{\"index\":3}]}"), ctx));
        var idx = await d.GetChild("steps[0].index");
        await Assert.That((await idx.Value())?.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task ClrJsonElement_MissingKey_IsNotFound()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(Json("{\"a\":1}"), ctx));
        var r = await d.GetChild("nope");
        await Assert.That(r.IsInitialized).IsFalse();
    }

    [Test]
    public async Task ClrPoco_FallsBackToReflection()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(new Poco { Label = "hi" }, ctx));
        await Assert.That((await (await d.GetChild("Label")).Value())?.ToString()).IsEqualTo("hi");
    }

    [Test]
    public async Task ClrJson_ConvertsToDict_OutboundOwnsIt()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(Json("{\"a\":1}"), ctx));
        var dict = await d.Convert("dict");
        await Assert.That(dict.Success).IsTrue();
        await Assert.That((await (await dict.GetChild("a")).Value())?.ToString()).IsEqualTo("1");
    }

    [Test]
    public async Task ClrJson_SerializesAsRawJson_NoValueKindLeak()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(Json("{\"a\":1,\"b\":[2,3]}"), ctx));
        var json = new global::app.channel.serializer.plang.@this(ctx).Serialize(d).Peek()!.ToString()!;
        await Assert.That(json).Contains("\"a\":1");
        await Assert.That(json).Contains("\"b\":[2,3]");
        await Assert.That(json.ToLowerInvariant()).DoesNotContain("valuekind");
    }

    private sealed class Poco { public string Label { get; set; } = ""; }
}
