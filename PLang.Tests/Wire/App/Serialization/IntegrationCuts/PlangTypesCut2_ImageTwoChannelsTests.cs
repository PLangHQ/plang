using System.Text.Json;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// plang-types — Integration cut 2: same value, two channels, two wire shapes.
// One image instance, driven through two writers — text and json — gives its size
// and a base64 string respectively. The image writes its bytes; each writer decides
// how bytes look.

public class PlangTypesCut2_ImageTwoChannelsTests
{
    private static readonly byte[] PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private sealed class CaptureWriter : global::app.type.format.IWriter
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

    [Test] public async Task SameImage_TextWriter_GivesItsSize()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2t-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var p = global::app.type.item.path.@this.Resolve("/srv/photo.png", app.actor.list.User.Context);
        var img = new image(PngBytes, p!, app.actor.list.User.Context);

        using var ms = new System.IO.MemoryStream();
        img.Write(new global::app.type.item.text.Writer(ms, System.Text.Encoding.UTF8, global::app.type.item.culture.@this.Create("en-US")!));
        await Assert.That(System.Text.Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo($"[{PngBytes.Length} bytes]");
    }

    [Test] public async Task SameImage_JsonWriter_GivesBase64String()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2j-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var img = new image(PngBytes, "image/png");

        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.item.kind.json.Writer(utf, view: global::app.View.Out);
            w.Value(img);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("\"" + System.Convert.ToBase64String(PngBytes) + "\"");
    }

    [Test] public async Task SameInstance_TwoWriters_NeverReMaterializesValue()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2s-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var p = global::app.type.item.path.@this.Resolve("/srv/x.png", app.actor.list.User.Context);
        var img = new image(PngBytes, p!, app.actor.list.User.Context);
        var beforeBytes = img.Bytes;

        img.Write(new CaptureWriter("text"));
        img.Write(new CaptureWriter("json"));

        // Bytes is the same reference — no copy, no re-decode.
        await Assert.That(ReferenceEquals(img.Bytes, beforeBytes)).IsTrue();
    }

    [Test] public async Task ImageInstance_DataTypeStaysImage_AcrossBothChannels()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut2i-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var img = new image(PngBytes, "image/png");
        var data = new global::app.data.@this("photo", img,
            new global::app.type.@this("image"), context: app.actor.list.User.Context);

        await Assert.That(data.Type?.Name).IsEqualTo("image");
        img.Write(new CaptureWriter("text"));
        await Assert.That(data.Type?.Name).IsEqualTo("image");
        img.Write(new CaptureWriter("json"));
        await Assert.That(data.Type?.Name).IsEqualTo("image");
    }
}
