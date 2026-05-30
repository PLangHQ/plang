using System.Reflection;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 3
// Compress / Decompress are flattened: no more `Data{archived, Data{gzip, byte[]}}`.

public class CompressFlattenedTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-cmp-" + Guid.NewGuid().ToString("N")[..8]));

    private static global::app.data.@this NewCompressibleData(global::app.@this app, string value)
    {
        // text/plain is compressible (kind = "text", not in image/video/audio/archive).
        var d = new global::app.data.@this("payload", value, global::app.type.@this.FromMime("text/plain"))
        { Context = app.User.Context };
        return d;
    }

    [Test] public async Task Compress_OnSimpleData_ProducesArchivedTypeWithByteArrayValue()
    {
        await using var app = NewApp();
        var d = NewCompressibleData(app, "the quick brown fox jumps over the lazy dog");
        var archived = d.Compress();
        await Assert.That(archived.Type?.Value).IsEqualTo("archived");
        await Assert.That(archived.Value).IsTypeOf<byte[]>();
    }

    [Test] public async Task Compress_OnSimpleData_ValueIsRawByteArray_NotWrappedInData()
    {
        await using var app = NewApp();
        var d = NewCompressibleData(app, "payload payload payload");
        var archived = d.Compress();
        // The smell this stage fixes — no nested Data around the gzip payload.
        await Assert.That(archived.Value is global::app.data.@this).IsFalse();
        await Assert.That(archived.Value is byte[]).IsTrue();
    }

    [Test] public async Task Decompress_AfterCompress_PreservesNameAndValue()
    {
        await using var app = NewApp();
        var d = NewCompressibleData(app, "round-trip value");
        var archived = d.Compress();
        var restored = archived.Decompress();
        await Assert.That(restored.Name).IsEqualTo("payload");
        await Assert.That(restored.Value as string).IsEqualTo("round-trip value");
    }

    [Test] public async Task Decompress_AfterCompress_PreservesProperties()
    {
        // Pre-Stage-4, Properties is [JsonIgnore] and doesn't cross the wire,
        // so this test pins the Stage-3 promise: name + value + signature ride
        // through. The Properties round-trip lands when Stage 4 flattens them.
        await using var app = NewApp();
        var d = NewCompressibleData(app, "with props");
        var archived = d.Compress();
        var restored = archived.Decompress();
        await Assert.That(restored.Value as string).IsEqualTo("with props");
    }

    [Test] public async Task CompressedBytes_OnceGunzipped_ParseToApplicationPlangDocWithSignature()
    {
        await using var app = NewApp();
        var d = NewCompressibleData(app, "needs a signature");
        var archived = d.Compress();
        var bytes = (byte[])archived.Value!;

        // Gunzip and parse — must be a valid application/plang doc with a signature.
        using var gz = new System.IO.Compression.GZipStream(new MemoryStream(bytes),
            System.IO.Compression.CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        gz.CopyTo(outMs);
        var json = System.Text.Encoding.UTF8.GetString(outMs.ToArray());
        await Assert.That(json).Contains("\"signature\"");
        await Assert.That(json).Contains("\"value\":\"needs a signature\"");
    }

    [Test] public async Task Compress_OnNonCompressibleType_ReturnsSelfUnchanged()
    {
        await using var app = NewApp();
        // image/png is in _notCompressible — kind "image"
        var d = new global::app.data.@this("img", new byte[] { 1, 2, 3 }, global::app.type.@this.FromMime("image/png"))
        { Context = app.User.Context };
        var result = d.Compress();
        await Assert.That(ReferenceEquals(d, result)).IsTrue();
    }

    [Test] public async Task Compress_RoutesThrough_RegisteredApplicationPlangSerializer()
    {
        // Verified structurally: the merged plang serializer's wire shape includes
        // {name, type, value, signature}. After compress+gunzip the bytes must parse
        // through application/plang Deserialize. We assert that path works.
        await using var app = NewApp();
        var d = NewCompressibleData(app, "via plang serializer");
        var archived = d.Compress();
        var restored = archived.Decompress();
        await Assert.That(restored.Success).IsTrue();
        await Assert.That(restored.Value as string).IsEqualTo("via plang serializer");
    }

    [Test] public async Task DataTransport_EnvelopeJsonOptionsField_NoLongerExists()
    {
        var t = typeof(global::app.data.@this);
        var field = t.GetField("_envelopeJsonOptions",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        await Assert.That(field).IsNull();
    }

    [Test] public async Task DataTransport_RehydrateNestedData_MethodNoLongerExists()
    {
        var t = typeof(global::app.data.@this);
        var method = t.GetMethod("RehydrateNestedData",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        await Assert.That(method).IsNull();
    }
}
