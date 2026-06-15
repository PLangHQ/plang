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
        var wire = (await plang.Serialize(d2).Value())!.Clr<string>()!;

        using var doc = JsonDocument.Parse(wire);
        // `type` is the structured entity {name, kind?, strict?} on the wire.
        await Assert.That(doc.RootElement.GetProperty("type").GetProperty("name").GetString()).IsEqualTo("archive");
        await Assert.That(doc.RootElement.GetProperty("value").ValueKind).IsEqualTo(JsonValueKind.String);
        await Assert.That(doc.RootElement.TryGetProperty("signature", out _)).IsTrue();
    }

    [Test] public async Task Cut2_InnerBytes_DecodeToSignedInnerDataWithOwnSignature()
    {
        await using var app = NewApp();
        var d1 = MakeCompressible(app, "Ingi");
        var d2 = d1.Compress();
        var inner = ((global::app.type.archive.@this)(await d2.Value())!).Value;

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
        // binding label off the archived wire; value + inner signature survive
        await Assert.That(restored.Name).IsEqualTo("");
        await Assert.That((await restored.Value())?.ToString()).IsEqualTo("Ingi");
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
        var wire = (await plang.Serialize(d2).Value())!.Clr<string>()!;

        // Flip a byte in the base64-encoded value — read back, verify must fail.
        using var doc = JsonDocument.Parse(wire);
        var b64 = doc.RootElement.GetProperty("value").GetString()!;
        var flipped = b64[0] == 'A' ? "B" + b64.Substring(1) : "A" + b64.Substring(1);
        var tampered = wire.Replace("\"value\":\"" + b64 + "\"", "\"value\":\"" + flipped + "\"");
        await Assert.That(tampered).IsNotEqualTo(wire);

        var back = plang.Deserialize(tampered);
        await back.IsSuccess();
        var restored = back;
        restored.Context = app.User.Context;

        var verify = await app.RunAction<global::app.module.signing.verify>(
            new global::app.module.signing.verify
            {
                Data = restored,
                SkipFreshnessCheck = new global::app.data.@this<global::app.type.@bool.@this>("", true)
            }, app.User.Context);
        await verify.IsFailure();
    }
}
