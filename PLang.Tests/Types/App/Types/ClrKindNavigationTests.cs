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
        var idx = await d.Get("steps[0].index");
        await Assert.That((await idx.Value())?.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task ClrJsonElement_MissingKey_IsNotFound()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(Json("{\"a\":1}"), ctx));
        var r = await d.Get("nope");
        await Assert.That(r.IsInitialized).IsFalse();
    }

    [Test]
    public async Task ClrPoco_FallsBackToReflection()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(new Poco { Label = "hi" }, ctx));
        await Assert.That((await (await d.Get("Label")).Value())?.ToString()).IsEqualTo("hi");
    }

    [Test]
    public async Task ClrJson_ConvertsToDict_OutboundOwnsIt()
    {
        var ctx = _app.User.Context;
        var d = ctx.Ok(new global::app.type.clr.@this(Json("{\"a\":1}"), ctx));
        var dict = await d.Convert(ctx.App.Type.Kind["dict"]);
        await Assert.That(dict.Success).IsTrue();
        await Assert.That((await (await dict.Get("a")).Value())?.ToString()).IsEqualTo("1");
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

    [Test]
    public async Task ClrEntity_IsRegistered_WithCarrierClrType()
    {
        // The identity door's last rung returns this["clr"] — so "clr" MUST resolve to the carrier
        // entity, else the door throws on the name miss. ClrType is the carrier so its Create builds one.
        var entity = _app.Type["clr"];
        await Assert.That(entity.Name).IsEqualTo("clr");
        await Assert.That(entity.ClrType).IsEqualTo(typeof(global::app.type.clr.@this));
    }

    [Test]
    public async Task Indexer_UnownedPoco_AnswersClrEntity_NeverNull()
    {
        // A CLR type no value type owns is not an item and not _clr-owned → the door's third rung
        // answers the clr entity (never null), whose Create builds the carrier.
        var entity = _app.Type[typeof(Poco)];
        await Assert.That(entity.Name).IsEqualTo("clr");
    }

    [Test]
    public async Task ApexLift_UnownedPoco_BecomesClrCarrier_Terminates()
    {
        var ctx = _app.User.Context;
        // The lift routes an unowned object through the clr entity's Create — terminal, no bounce back
        // into the lift. Completing at all is the no-recursion proof; the carrier still navigates.
        var lifted = global::app.type.item.@this.Create(new Poco { Label = "hi" }, ctx);
        await Assert.That(lifted).IsTypeOf<global::app.type.clr.@this>();
        await Assert.That((await (await ctx.Ok(lifted).Get("Label")).Value())?.ToString()).IsEqualTo("hi");
    }

    [Test]
    public async Task ApexLift_NonItemNamedHost_BecomesClrCarrier_NoRecursion()
    {
        var ctx = _app.User.Context;
        // A non-item host (the type registry itself) — rung 2's item⟺ICreate guard sends it to the clr
        // entity instead of resurrecting a non-Creatable named entity whose decline used to loop.
        var lifted = global::app.type.item.@this.Create(ctx.App.Type, ctx);
        await Assert.That(lifted).IsTypeOf<global::app.type.clr.@this>();
    }

    [Test]
    public async Task KindProbe_UnownedParam_BuildsClrCarrier_SoProbeSkipsStamp()
    {
        var ctx = _app.User.Context;
        // The build-time kind probe stamps a param ONLY when the built value has its own item type.
        // An unowned param answers the clr entity → a clr carrier → the probe's `is not clr` guard
        // leaves the param on its declared type instead of stamping a bogus item/* kind.
        var clrEntity = _app.Type[typeof(Poco)];
        var built = clrEntity.Create("anything", ctx.Ok(new global::app.type.item.@null.@this(clrEntity.Name)));
        await Assert.That(built).IsTypeOf<global::app.type.clr.@this>();

        // Contrast: an owned type builds its own value with a real kind → the probe stamps it.
        var numBuilt = _app.Type["number"].Create("5", ctx.Ok(new global::app.type.item.@null.@this("number")));
        await Assert.That(numBuilt is global::app.type.clr.@this).IsFalse();
        await Assert.That(numBuilt!.Type.Kind).IsNotNull();
    }

    private sealed class Poco { public string Label { get; set; } = ""; }
}
