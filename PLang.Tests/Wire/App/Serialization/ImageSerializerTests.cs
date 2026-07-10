using System.Text.Json;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 5 (the format-asymmetric proof)
// image/serializer/text.cs → path placeholder.
// image/serializer/protobuf.cs → raw bytes (stub until protobuf writer ships).
// image/serializer/Default.cs → base64 (covers json + plang).
// One Image instance, three wire shapes by writer Format token.

public class ImageSerializerTests
{
    private static readonly byte[] PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private sealed class CaptureWriter : global::app.channel.serializer.IWriter
    {
        public string Format { get; }
        public object? Last { get; private set; }
        public string LastMethod { get; private set; } = "";
        public CaptureWriter(string format) { Format = format; }
        public void Null() { LastMethod = "Null"; }
        public void Bool(bool v) { }
        public void Int(int v) { }
        public void Long(long v) { }
        public void Float(float v) { }
        public void Double(double v) { }
        public void String(string v) { LastMethod = "String"; Last = v; }
        public void DateTime(System.DateTime v) { }
        public void DateTimeOffset(System.DateTimeOffset v) { }
        public void TimeSpan(System.TimeSpan v) { }
        public void Guid(System.Guid v) { }
        public void Enum(System.Enum v) { }
        public void Decimal(decimal v) { }
        public void Bytes(byte[] v) { LastMethod = "Bytes"; Last = v; }
        public void BeginArray(int c) { }
        public void BeginObject() { LastMethod = "BeginObject"; }
        public void Name(string n) { LastMethod = "Name"; Last = n; }
        public void EndObject() { LastMethod = "EndObject"; }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this r) { }
        public void EndRecord() { }
        public void Value(object? n) { }
    }

    [Test] public async Task Image_TextFormat_RendersPathPlaceholder()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-imgs-" + System.Guid.NewGuid().ToString("N")[..8]));
        var p = global::app.type.item.path.@this.Resolve("/some/photo.png", app.User.Context);
        var img = new image(PngBytes, "image/png", p);
        var w = new CaptureWriter("text");
        global::app.type.item.image.serializer.text.Write(img, w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(((string)w.Last!).Contains("photo.png") || ((string)w.Last!).Contains("image:")).IsTrue();
    }

    [Test] public async Task Image_TextFormat_Base64Source_PlaceholderIsBareLabel()
    {
        // No Path → text writer falls back to the bare label.
        var img = new image(PngBytes, "image/png");
        var w = new CaptureWriter("text");
        global::app.type.item.image.serializer.text.Write(img, w);
        await Assert.That(((string)w.Last!).Contains("[image:")).IsTrue();
    }

    [Test] public async Task Image_JsonFormat_DefaultFallback_RendersBase64()
    {
        var img = new image(PngBytes, "image/png");
        var w = new CaptureWriter("json");
        global::app.type.item.image.serializer.Default.Write(img, w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(w.Last).IsEqualTo(System.Convert.ToBase64String(PngBytes));
    }

    [Test] public async Task Image_PlangFormat_DefaultFallback_RendersBase64()
    {
        var renderers = new global::app.type.renderer.@this();
        // plang Format falls through to wildcard "*" → Default → base64.
        var write = renderers.Of("image", "plang");
        await Assert.That(write).IsNotNull();
        var w = new CaptureWriter("plang");
        write!(new image(PngBytes, "image/png"), w);
        await Assert.That(w.Last).IsEqualTo(System.Convert.ToBase64String(PngBytes));
    }

    [Test] public async Task Image_ProtobufFormat_RendersRawBytes_StubInPlace()
    {
        var renderers = new global::app.type.renderer.@this();
        var write = renderers.Of("image", "protobuf");
        await Assert.That(write).IsNotNull();
        var w = new CaptureWriter("protobuf");
        write!(new image(PngBytes, "image/png"), w);
        await Assert.That(w.LastMethod).IsEqualTo("Bytes");
        await Assert.That(w.Last).IsEqualTo(PngBytes);
    }

    [Test] public async Task Image_RoundTrip_JsonBase64_PreservesBytesAndMime()
    {
        // Write → base64 string. Round-trip back via image.Resolve(byte[]).
        var img = new image(PngBytes, "image/png");
        var renderers = new global::app.type.renderer.@this();
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: renderers);
            w.Value(img);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        // The JSON is a base64 string literal.
        await Assert.That(json.StartsWith("\"")).IsTrue();
        // Decode back.
        var b64 = json.Trim('"');
        var roundTripped = image.FromBytes(System.Convert.FromBase64String(b64));
        await Assert.That(roundTripped!.Mime).IsEqualTo("image/png");
        await Assert.That(roundTripped.Bytes.SequenceEqual(PngBytes)).IsTrue();
    }

    [Test] public async Task Image_SerializerCoverage_PassesPlngGate()
    {
        // image has Default.cs + text.cs + protobuf.cs — three files, the
        // (type, *) wildcard covers any format that doesn't ship a dedicated
        // file. The would-be PLNG gate accepts it.
        var renderers = new global::app.type.renderer.@this();
        await Assert.That(renderers.Has("image")).IsTrue();
        await Assert.That(renderers.Of("image", "json")).IsNotNull();
        await Assert.That(renderers.Of("image", "text")).IsNotNull();
        await Assert.That(renderers.Of("image", "protobuf")).IsNotNull();
    }
}
