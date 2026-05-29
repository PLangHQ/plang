using System.Text.Json;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 2: Sign-then-compress preserves inner attestation.

public class Cut2_SignThenCompressTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-cut2-" + Guid.NewGuid().ToString("N")[..8]));

    private static global::app.data.@this MakeCompressible(global::app.@this app, string payload)
        => new global::app.data.@this("user", payload, global::app.type.@this.FromMime("text/plain"))
        { Context = app.User.Context };

    [Test] public async Task Cut2_OuterWireJson_HasArchivedTypeBytesValueAndSignature()
    {
        await using var app = NewApp();
        var d1 = MakeCompressible(app, "Ingi");
        var d2 = d1.Compress();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        var wire = plang.Serialize(d2).Value!;

        using var doc = JsonDocument.Parse(wire);
        await Assert.That(doc.RootElement.GetProperty("type").GetString()).IsEqualTo("archived");
        await Assert.That(doc.RootElement.GetProperty("value").ValueKind).IsEqualTo(JsonValueKind.String);
        await Assert.That(doc.RootElement.TryGetProperty("signature", out _)).IsTrue();
    }

    [Test] public async Task Cut2_InnerBytes_DecodeToSignedInnerDataWithOwnSignature()
    {
        await using var app = NewApp();
        var d1 = MakeCompressible(app, "Ingi");
        var d2 = d1.Compress();
        var inner = (byte[])d2.Value!;

        using var gz = new System.IO.Compression.GZipStream(new MemoryStream(inner),
            System.IO.Compression.CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        gz.CopyTo(outMs);
        var json = System.Text.Encoding.UTF8.GetString(outMs.ToArray());
        await Assert.That(json).Contains("\"signature\"");
        await Assert.That(json).Contains("\"value\":\"Ingi\"");
    }

    [Test] public async Task Cut2_Decompress_ReturnsOriginalWithInnerSignaturePreserved()
    {
        await using var app = NewApp();
        var d1 = MakeCompressible(app, "Ingi");
        var d2 = d1.Compress();
        var restored = d2.Decompress();
        await Assert.That(restored.Name).IsEqualTo("user");
        await Assert.That(restored.Value as string).IsEqualTo("Ingi");
        await Assert.That(restored.Signature).IsNotNull()
            .Because("Inner signature was populated when Compress wrote bytes through the wire converter.");
    }

    [Test] public async Task Cut2_TamperingValueByte_FailsOuterSignatureVerify()
    {
        await using var app = NewApp();
        var d1 = MakeCompressible(app, "Ingi");
        var d2 = d1.Compress();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        var wire = plang.Serialize(d2).Value!;

        // Flip a byte in the base64-encoded value — read back, verify must fail.
        using var doc = JsonDocument.Parse(wire);
        var b64 = doc.RootElement.GetProperty("value").GetString()!;
        var flipped = b64[0] == 'A' ? "B" + b64.Substring(1) : "A" + b64.Substring(1);
        var tampered = wire.Replace("\"value\":\"" + b64 + "\"", "\"value\":\"" + flipped + "\"");
        await Assert.That(tampered).IsNotEqualTo(wire);

        var back = plang.Deserialize(tampered);
        await Assert.That(back.Success).IsTrue();
        var restored = (global::app.data.@this)back.Value!;
        restored.Context = app.User.Context;

        var verify = await app.RunAction<global::app.module.signing.verify>(
            new global::app.module.signing.verify
            {
                Data = restored,
                SkipFreshnessCheck = new global::app.data.@this<bool>("", true)
            }, app.User.Context);
        await Assert.That(verify.Success).IsFalse();
    }
}
