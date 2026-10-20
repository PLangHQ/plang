using System.Text.Json;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Serialization;

// An image writes its bytes through the writer's Bytes primitive; each writer decides
// how bytes look. An image not read yet writes its path.

public class ImageSerializerTests
{
    private static readonly byte[] PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private sealed class CaptureWriter : global::app.type.format.IWriter
    {
        public global::app.type.kind.@this Kind { get; }
        public object? Last { get; private set; }
        public string LastMethod { get; private set; } = "";
        public CaptureWriter(global::app.type.kind.@this kind) { Kind = kind; }
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

    // The image states what it has; the format is not its question.
    [Test] public async Task Image_Loaded_WritesItsBytes_WhateverTheFormat()
    {
        await using var app = new global::app.@this("/test").Testing();
        var kinds = new[]
        {
            app.type.list["text"].kind,
            app.type.list["item"].kind["json"]!,
            app.type.list["wire"].kind["plang"]!,
            app.type.list["list"].kind["formal"]!,
        };
        foreach (var kind in kinds)
        {
            var w = new CaptureWriter(kind);
            await w.Kind.pair.Output(app.actor.list.User.Context.Image(PngBytes, "image/png"), w, global::app.View.Store, app.actor.list.User.Context);
            await Assert.That(w.LastMethod).IsEqualTo("Bytes");
            await Assert.That(w.Last).IsEqualTo(PngBytes);
        }
    }

    [Test] public async Task Image_LoadedWithPath_StillWritesItsBytes()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-imgs-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var p = global::app.type.item.path.@this.Resolve("/some/photo.png", app.actor.list.User.Context);
        var w = new CaptureWriter(app.type.list["text"].kind);
        await w.Kind.pair.Output(app.actor.list.User.Context.Image(p!, PngBytes), w, global::app.View.Store, app.actor.list.User.Context);
        await Assert.That(w.LastMethod).IsEqualTo("Bytes");
        await Assert.That(w.Last).IsEqualTo(PngBytes);
    }

    [Test] public async Task Image_UnreadPathBacked_WritesItsPath()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-imgs-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var p = global::app.type.item.path.@this.Resolve("/some/photo.png", app.actor.list.User.Context);
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!);
        await w.Kind.pair.Output(app.actor.list.User.Context.Image(p!), w, global::app.View.Store, app.actor.list.User.Context);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That((string)w.Last!).Contains("photo.png");
    }

    [Test] public async Task Image_RoundTrip_JsonBase64_PreservesBytesAndMime()
    {
        // Write → base64 string. Round-trip back via image.FromBytes.
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var img = ctx.Image(PngBytes, "image/png");
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.item.kind.json.Writer(utf, app.type.list, view: global::app.View.Out);
            await w.Kind.pair.Output(img, w, global::app.View.Out, ctx);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        // The JSON is a base64 string literal.
        await Assert.That(json.StartsWith("\"")).IsTrue();
        // Decode back.
        var b64 = json.Trim('"');
        var roundTripped = image.FromBytes(System.Convert.FromBase64String(b64), ctx);
        await Assert.That(roundTripped!.Mime).IsEqualTo("image/png");
        await Assert.That(roundTripped.Bytes.SequenceEqual(PngBytes)).IsTrue();
    }
}
