using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using DataT = global::app.data.@this;
using TypeEntity = global::app.type.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// The wire emits flat `type` + `kind` keys (NOT `type:kind`, NOT `"type":"null"`).
// `Data.Kind` is sourced from `Type.Kind` — one home — but the serialised shape
// keeps two flat keys for backward compatibility.
public class WireKindShapeTests
{
    private static JsonSerializerOptions Options
        => global::app.channel.serializer.plang.@this.ContextLessFallback
            .GetType()
            .GetField("_outbound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(global::app.channel.serializer.plang.@this.ContextLessFallback)
            as JsonSerializerOptions
            ?? throw new System.InvalidOperationException("could not access plang outbound options");

    private static string ToJson(DataT data) => JsonSerializer.Serialize(data, Options);
    private static DataT FromJson(string json) => JsonSerializer.Deserialize<DataT>(json, Options)!;

    [Test] public async Task Wire_Write_EmitsFlatTypeAndKindKeys()
    {
        var d = new DataT("x", "hi", new TypeEntity("text", "md"));
        var json = ToJson(d);
        await Assert.That(json.Contains("\"type\":\"text\"")).IsTrue();
        await Assert.That(json.Contains("\"kind\":\"md\"")).IsTrue();
    }

    [Test] public async Task Wire_Write_OmitsKindKey_WhenNull()
    {
        var d = new DataT("x", "hi", new TypeEntity("text"));
        var json = ToJson(d);
        await Assert.That(json.Contains("\"type\":\"text\"")).IsTrue();
        await Assert.That(json.Contains("\"kind\"")).IsFalse();
    }

    [Test] public async Task Wire_Write_NoTypeColonKindCompositeString()
    {
        var d = new DataT("x", "hi", new TypeEntity("text", "md"));
        var json = ToJson(d);
        await Assert.That(json.Contains("text:md")).IsFalse();
        await Assert.That(json.Contains("\"text/md\"")).IsFalse();
        await Assert.That(json.Contains("\"type\":\"text/md\"")).IsFalse();
    }

    [Test] public async Task Wire_RoundTrip_PreservesNameKindStrict()
    {
        // Strict is not part of the wire shape (it's a build-time bit); the
        // round-trip preserves Name + Kind. Pin both, and assert Strict's
        // wire-absence by checking it stays its default after deserialise.
        var d = new DataT("x", "data", new TypeEntity("image", "gif") { });
        var json = ToJson(d);
        var roundTripped = FromJson(json);
        await Assert.That(roundTripped.Type.Name).IsEqualTo("image");
        await Assert.That(roundTripped.Type.Kind).IsEqualTo("gif");
        await Assert.That(roundTripped.Type.Strict).IsFalse();  // not on the wire
    }

    [Test] public async Task Wire_Read_LegacyPrWithSeparateTypeAndKindFields_Deserializes()
    {
        // The legacy shape is the SAME shape post-fold (the fold is internal,
        // wire shape unchanged): "type":"text", "kind":"md" as two top-level
        // fields on Data. Pin that a hand-written legacy doc still parses.
        var legacy = "{\"name\":\"x\",\"type\":\"text\",\"kind\":\"md\",\"value\":\"hi\"}";
        var d = FromJson(legacy);
        await Assert.That(d.Type.Name).IsEqualTo("text");
        await Assert.That(d.Type.Kind).IsEqualTo("md");
        await Assert.That(d.Kind).IsEqualTo("md");
    }

    [Test] public async Task Wire_Read_NoTypeNullStringEmitted()
    {
        // Null sentinel Type: wire emits no "type" key, and no "type":"null".
        var d = new DataT("x", null, TypeEntity.Null);
        var json = ToJson(d);
        await Assert.That(json.Contains("\"type\":\"null\"")).IsFalse();
        await Assert.That(json.Contains("\"type\"")).IsFalse();
    }
}
