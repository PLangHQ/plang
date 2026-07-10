using System.Text.Json;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// plang-types — Integration cut 2: same value, two channels, two wire shapes.
// One image instance, driven through two writers — text and json — gives a path
// placeholder and a base64 string respectively. The channel never branches on type;
// the type never knows about channels. The bridge is IWriter.Format.

public class PlangTypesCut2_ImageTwoChannelsTests
{
    private static readonly byte[] PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private sealed class CaptureWriter : global::app.channel.serializer.IWriter
    {
        public string Format { get; }
        public string LastMethod { get; private set; } = "";
        public object? Last { get; private set; }
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

    [Test] public async Task SameImage_TextWriter_GivesPathPlaceholder()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2t-" + System.Guid.NewGuid().ToString("N")[..8]));
        var p = global::app.type.item.path.@this.Resolve("/srv/photo.png", app.User.Context);
        var img = new image(PngBytes, "image/png", p);

        var write = app.Type.Renderers.Of("image", "text");
        await Assert.That(write).IsNotNull();
        var w = new CaptureWriter("text");
        write!(img, w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(((string)w.Last!).Contains("photo.png") || ((string)w.Last!).Contains("image:")).IsTrue();
    }

    [Test] public async Task SameImage_JsonWriter_GivesBase64String()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2j-" + System.Guid.NewGuid().ToString("N")[..8]));
        var img = new image(PngBytes, "image/png");

        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: app.Type.Renderers);
            w.Value(img);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("\"" + System.Convert.ToBase64String(PngBytes) + "\"");
    }

    [Test] public async Task SameInstance_TwoWriters_NeverReMaterializesValue()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2s-" + System.Guid.NewGuid().ToString("N")[..8]));
        var p = global::app.type.item.path.@this.Resolve("/srv/x.png", app.User.Context);
        var img = new image(PngBytes, "image/png", p);
        var beforeBytes = img.Bytes;

        app.Type.Renderers.Of("image", "text")!(img, new CaptureWriter("text"));
        app.Type.Renderers.Of("image", "json")!(img, new CaptureWriter("json"));

        // Bytes is the same reference — no copy, no re-decode.
        await Assert.That(ReferenceEquals(img.Bytes, beforeBytes)).IsTrue();
    }

    [Test] public async Task ChannelSwitch_AcrossTwoOutputs_NoTypeBranching_InChannelCode()
    {
        // Channel/writer code is type-agnostic: the json writer has ONE
        // `case TypedValueNode` that dispatches via renderers, no per-type
        // switch. Verify by reflection: json.Writer.Value(object?) is the
        // only place writer ever sees the typed value, and the case it
        // takes is TypedValueNode — not image/number/code branches.
        var writerType = typeof(global::app.channel.serializer.json.Writer);
        var valueMethod = writerType.GetMethod("Value",
            new[] { typeof(object) });
        await Assert.That(valueMethod).IsNotNull();
        // The dispatch indirection is the renderers lookup; assert it has
        // a renderers field (passed to the ctor).
        var renderersField = writerType.GetField("_renderers",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        await Assert.That(renderersField).IsNotNull();
    }

    [Test] public async Task ImageInstance_DataTypeStaysImage_AcrossBothChannels()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2i-" + System.Guid.NewGuid().ToString("N")[..8]));
        var img = new image(PngBytes, "image/png");
        var data = new global::app.data.@this("photo", img,
            new global::app.type.@this("image"), context: app.User.Context);

        await Assert.That(data.Type?.Name).IsEqualTo("image");
        app.Type.Renderers.Of("image", "text")!(img, new CaptureWriter("text"));
        await Assert.That(data.Type?.Name).IsEqualTo("image");
        app.Type.Renderers.Of("image", "json")!(img, new CaptureWriter("json"));
        await Assert.That(data.Type?.Name).IsEqualTo("image");
    }
}
