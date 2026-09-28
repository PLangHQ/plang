using System.Text.Json;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 5 (the format-asymmetric proof)
// An image writes itself by the writer's Format token:
// text → path placeholder; protobuf → raw bytes (stub until protobuf writer ships);
// anything else → base64 (covers json + plang).
// One Image instance, three wire shapes.

public class ImageSerializerTests
{
    private static readonly byte[] PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private sealed class CaptureWriter : global::app.type.format.IWriter
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
        var p = global::app.type.item.path.@this.Resolve("/some/photo.png", app.actor.list.User.Context);
        var img = new image(PngBytes, p!, app.actor.list.User.Context);
        var w = new CaptureWriter("text");
        img.Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(((string)w.Last!).Contains("photo.png") || ((string)w.Last!).Contains("image:")).IsTrue();
    }

    [Test] public async Task Image_TextFormat_Base64Source_PlaceholderIsBareLabel()
    {
        // No Path → text writer falls back to the bare label.
        var img = new image(PngBytes, "image/png");
        var w = new CaptureWriter("text");
        img.Write(w);
        await Assert.That(((string)w.Last!).Contains("[image:")).IsTrue();
    }

    [Test] public async Task Image_JsonFormat_DefaultFallback_RendersBase64()
    {
        var img = new image(PngBytes, "image/png");
        var w = new CaptureWriter("json");
        img.Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(w.Last).IsEqualTo(System.Convert.ToBase64String(PngBytes));
    }

    [Test] public async Task Image_PlangFormat_DefaultFallback_RendersBase64()
    {
        // plang Format takes the portable default → base64.
        var w = new CaptureWriter("plang");
        new image(PngBytes, "image/png").Write(w);
        await Assert.That(w.Last).IsEqualTo(System.Convert.ToBase64String(PngBytes));
    }

    [Test] public async Task Image_ProtobufFormat_RendersRawBytes_StubInPlace()
    {
        var w = new CaptureWriter("protobuf");
        new image(PngBytes, "image/png").Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("Bytes");
        await Assert.That(w.Last).IsEqualTo(PngBytes);
    }

    [Test] public async Task Image_RoundTrip_JsonBase64_PreservesBytesAndMime()
    {
        // Write → base64 string. Round-trip back via image.Resolve(byte[]).
        var img = new image(PngBytes, "image/png");
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.format.json.Writer(utf, view: global::app.View.Out);
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
}
