using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// The @schema layer model — a signed value is a `signature` LAYER wrapping the
// inner `data`, not a Data with a sidecar `signature` property. This pins the
// confirmed flat wire shape that signature.@this.Write renders through the
// IWriter object surface. Format confirmation (Ingi, 2026-06-15).

public class SchemaLayerFormatTests
{
    // Render a value through the json IWriter exactly as the wire writer's
    // value-slot dispatch does (case item.@this v: v.Write(this)).
    private static string Render(global::app.type.item.@this value)
    {
        using var ms = new MemoryStream();
        using (var jw = new System.Text.Json.Utf8JsonWriter(ms))
        {
            var writer = new global::app.channel.serializer.json.Writer(jw);
            writer.Value(value);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test] public async Task SignatureLayer_RendersFlat_SchemaSignature_WrappingInnerData()
    {
        var inner = new global::app.data.@this("user", "Ingi", global::app.type.@this.FromName("text"));
        var sig = new global::app.type.signature.@this(
            value: inner,
            algorithm: new global::app.type.text.@this("ed25519"),
            nonce: new global::app.type.text.@this("9f"),
            created: new global::app.type.datetime.@this(new System.DateTimeOffset(2026, 6, 15, 10, 0, 0, System.TimeSpan.Zero)),
            identity: new global::app.type.text.@this("alice"),
            hash: new global::app.module.crypto.type.hash.@this(System.Convert.FromBase64String("ZGlnZXN0"), "keccak256"),
            signature: new global::app.type.binary.@this(System.Convert.FromBase64String("c2ln")));

        var json = Render(sig);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // The outer object IS the signature layer — flat fields, @schema:signature.
        await Assert.That(root.GetProperty("@schema").GetString()).IsEqualTo("signature");
        await Assert.That(root.GetProperty("algorithm").GetString()).IsEqualTo("ed25519");
        await Assert.That(root.GetProperty("identity").GetString()).IsEqualTo("alice");
        await Assert.That(root.GetProperty("signature").GetString()).IsEqualTo("c2ln");
        // hash is a flat sub-object {type, value}.
        await Assert.That(root.GetProperty("hash").GetProperty("type").GetString()).IsEqualTo("keccak256");
        await Assert.That(root.GetProperty("hash").GetProperty("value").GetString()).IsEqualTo("ZGlnZXN0");

        // value holds the inner schema — itself a @schema:data record.
        var value = root.GetProperty("value");
        await Assert.That(value.GetProperty("@schema").GetString()).IsEqualTo("data");
        await Assert.That(value.GetProperty("value").GetString()).IsEqualTo("Ingi");
    }

    [Test] public async Task SignatureLayer_OmitsExpiresAndContracts_WhenAbsent()
    {
        var inner = new global::app.data.@this("x", "y", global::app.type.@this.FromName("text"));
        var sig = new global::app.type.signature.@this(
            value: inner,
            algorithm: new global::app.type.text.@this("ed25519"),
            nonce: new global::app.type.text.@this("n"),
            created: new global::app.type.datetime.@this(System.DateTimeOffset.UnixEpoch),
            identity: new global::app.type.text.@this("id"),
            hash: new global::app.module.crypto.type.hash.@this(new byte[] { 0x68 }, "keccak256"),
            signature: new global::app.type.binary.@this(new byte[] { 0x01 }));

        using var doc = JsonDocument.Parse(Render(sig));
        await Assert.That(doc.RootElement.TryGetProperty("expires", out _)).IsFalse();
        await Assert.That(doc.RootElement.TryGetProperty("contracts", out _)).IsFalse();
    }
}
