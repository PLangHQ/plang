using System.Text.Json;
using System.Text.Json.Serialization;
using app.channel.serializer;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Json (the JSON-engine custodian) gains composition extensions so callers compose
// with it instead of duplicating JsonSerializerOptions blocks.

public class JsonCompositionTests
{
    private sealed class Probe : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader r, System.Type t, JsonSerializerOptions o) => r.GetString() ?? "";
        public override void Write(Utf8JsonWriter w, string v, JsonSerializerOptions o) => w.WriteStringValue("probe:" + v);
    }

    [Test] public async Task Json_WithConverter_ReturnsNewInstance_WithConverterRegistered()
    {
        var json = new Json();
        var withProbe = json.WithConverter(new Probe());
        await Assert.That(withProbe).IsNotEqualTo(json);

        var wire = (await withProbe.Serialize(global::app.data.@this.Ok("hello")).Value())!.Value;
        await Assert.That(wire).Contains("probe:hello");
    }

    [Test] public async Task Json_WithConverter_DoesNotMutateOriginalInstance()
    {
        var json = new Json();
        var original = (await json.Serialize(global::app.data.@this.Ok("hello")).Value())!.Value;
        json.WithConverter(new Probe());
        var afterCompose = (await json.Serialize(global::app.data.@this.Ok("hello")).Value())!.Value;
        await Assert.That(original).IsEqualTo(afterCompose);
    }

    [Test] public async Task Json_WithModifier_ReturnsNewInstance_WithModifierOnResolver()
    {
        var json = new Json();
        bool fired = false;
        var withMod = json.WithModifier(_ => { fired = true; });
        // Drive serialization to invoke the resolver.
        withMod.Serialize(global::app.data.@this.Ok("x"));
        await Assert.That(fired).IsTrue();
        await Assert.That(withMod).IsNotEqualTo(json);
    }

    [Test] public async Task Json_WithModifier_DoesNotMutateOriginalInstance()
    {
        var json = new Json();
        bool firedOnOriginal = false;
        json.WithModifier(_ => { firedOnOriginal = true; });
        json.Serialize(global::app.data.@this.Ok("x"));
        await Assert.That(firedOnOriginal).IsFalse()
            .Because("Modifier should attach to the new instance, leaving the source's resolver intact.");
    }

    [Test] public async Task Json_ForInbound_AppliesTransportForInboundModifier()
    {
        var json = new Json();
        var inbound = json.ForInbound();
        await Assert.That(inbound).IsNotEqualTo(json);
    }

    [Test] public async Task Json_ForView_StillApplies_TransportForViewModifier()
    {
        var json = new Json();
        var view = json.ForView(global::app.View.Debug);
        await Assert.That(view).IsNotEqualTo(json);
    }
}
